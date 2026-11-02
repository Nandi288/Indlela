# Indlela

Indlela is a free web app that helps young South Africans learn a skill, prove it, and apply for jobs that need it. Learners follow a roadmap, work through free courses, build verified proof of their skills, and get certificates with QR codes that employers can scan.

Built by DOTNET NINJAS (Durban University of Technology) for the FNB App of the Year Hackathon 2026 under the Youth Employment and Work Readiness challenge.

Live site: https://indlela-theway-c5ewandbgbd4g0fe.southafricanorth-01.azurewebsites.net


## Features

- **Smart Start:** six questions recommend one of three roadmaps (Software Development, Digital Marketing, Accounting and Finance), with reasons and time needed.
- **Learning:** text-first lessons with short quizzes, plus links to free provider courses.
- **My path dashboard:** progress, streaks, badges, and a Forecast of the finish date.
- **Proof:** verified skills, certificates with short codes and QR codes, and a public verify page.
- **Portfolio and CV:** a public portfolio and a one-column, ATS-friendly CV.
- **Job matching:** nine entry-level roles, with match percentages and missing skills.
- **Tech Pulse:** South African tech news from RSS feeds, refreshed every six hours.

Indlela is not an accredited provider. Certificates are recognition of learning, not accredited qualifications.

## Tech stack

- ASP.NET Core 8 MVC (C#), Razor views
- Entity Framework Core 8 with SQL Server / Azure SQL
- ASP.NET Core Identity (email verification, account lockout)
- Background refresh service for RSS news
- Hosted on Microsoft Azure App Service
