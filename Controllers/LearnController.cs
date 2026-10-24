using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Demo;
using Indlela.Services.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers;

public class LessonVm
{
    public required Course Course { get; init; }
    public required LearnerEnrollment Enrollment { get; init; }
    public required DemoLesson[] Lessons { get; init; }
    public int Number { get; init; }             // 1-based lesson being shown; 0 = course outline
    public int DoneCount { get; init; }
    public int? ChosenAnswer { get; init; }
    public bool? AnsweredCorrectly { get; init; }
    public DemoLesson? Lesson => Number >= 1 && Number <= Lessons.Length ? Lessons[Number - 1] : null;
    public int NextNumber => Math.Min(DoneCount + 1, Lessons.Length);
}

/// <summary>In-app learning (UC-28 lesson player, UC-29 quizzes). Each passed lesson records verified InApp progress.</summary>
[Authorize]
[Route("learn")]
public class LearnController(IndlelaDbContext db, IProgressService progress, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("{courseId:int}")]
    public Task<IActionResult> Outline(int courseId, CancellationToken ct) => Show(courseId, 0, null, null, ct);

    [HttpGet("{courseId:int}/{number:int}")]
    public Task<IActionResult> Lesson(int courseId, int number, CancellationToken ct) => Show(courseId, number, null, null, ct);

    [HttpPost("{courseId:int}/{number:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(int courseId, int number, int? answer, CancellationToken ct)
    {
        var (course, enrollment, lessons) = await LoadAsync(courseId, ct);
        if (course is null) return NotFound();
        if (enrollment is null) return RedirectToAction("Details", "Courses", new { id = courseId, slug = course.Slug });
        if (number < 1 || number > lessons.Length) return NotFound();

        var lesson = lessons[number - 1];
        if (lesson.HasQuiz && answer != lesson.CorrectIndex)
            return await Show(courseId, number, answer, false, ct); // try again — nothing recorded

        var percentage = (int)Math.Round(100.0 * number / lessons.Length);
        var result = await progress.RecordAsync(enrollment.Id, users.GetUserId(User), $"Lesson {number}: {lesson.Title}",
            percentage, ProgressSource.InApp, ct);

        var badges = result.NewBadges is { Count: > 0 } nb ? " New badge: " + string.Join(", ", nb.Select(b => b.Name)) + "!" : "";

        if (number == lessons.Length)
        {
            TempData["Flash"] = $"Course complete — verified by in-app quizzes.{badges} Here's your certificate.";
            return Redirect($"/certificate/{enrollment.Id}");
        }

        TempData["Flash"] = $"Correct! Lesson {number} done — {percentage}% of the course, verified.{badges}";
        return Redirect($"/learn/{courseId}/{number + 1}");
    }

    private async Task<IActionResult> Show(int courseId, int number, int? chosen, bool? correct, CancellationToken ct)
    {
        var (course, enrollment, lessons) = await LoadAsync(courseId, ct);
        if (course is null) return NotFound();
        if (enrollment is null) return RedirectToAction("Details", "Courses", new { id = courseId, slug = course.Slug });

        var done = Math.Min(lessons.Length, (int)Math.Floor(enrollment.CurrentPercentage * lessons.Length / 100.0 + 0.001));
        if (number > done + 1) number = done + 1; // lessons unlock in order
        if (number > lessons.Length) number = 0;

        return View("Lesson", new LessonVm
        {
            Course = course, Enrollment = enrollment, Lessons = lessons, Number = number,
            DoneCount = done, ChosenAnswer = chosen, AnsweredCorrectly = correct
        });
    }

    private async Task<(Course? Course, LearnerEnrollment? Enrollment, DemoLesson[] Lessons)> LoadAsync(int courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == courseId && c.IsPublished && c.DeliveryMode == DeliveryMode.InApp, ct);
        if (course is null) return (null, null, []);
        var userId = users.GetUserId(User);
        var enrollment = await db.LearnerEnrollments.AsNoTracking()
            .SingleOrDefaultAsync(e => e.CourseId == courseId && e.UserId == userId && e.HandoffStatus == HandoffStatus.Enrolled, ct);
        return (course, enrollment, DemoCourseContent.For(course.Slug, course.Title, course.Description, course.Provider));
    }
}
