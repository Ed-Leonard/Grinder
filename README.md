# Grinder

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-10-239120?logo=csharp)
![SvelteKit](https://img.shields.io/badge/SvelteKit-2-FF3E00?logo=svelte)
![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178C6?logo=typescript)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-4-06B6D4?logo=tailwindcss)
![SQLite](https://img.shields.io/badge/SQLite-3-003B57?logo=sqlite)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql)
![SignalR](https://img.shields.io/badge/SignalR-ASP.NET_Core-512BD4?logo=dotnet)
![LiveKit](https://img.shields.io/badge/LiveKit-WebRTC-FF3E00?logo=livekit&logoColor=white)
![xUnit](https://img.shields.io/badge/xUnit-2.9-512BD4)

Grinder is a task management application that allows users to create an account, verify their email, and manage tasks organised into task lists.

## Technologies

### Front-end

- [SvelteKit](https://kit.svelte.dev/docs) - Front-end framework
- [Svelte 5](https://svelte.dev/docs) - Component framework
- [TypeScript](https://www.typescriptlang.org/docs/) - Language
- [Tailwind CSS 4](https://tailwindcss.com/docs) - Utility-first CSS framework
- [Vite](https://vite.dev/guide/) - Build tool
- [DayFlow](https://calendar.dayflow.studio/docs/introduction) - Calendar Library

### Back-end

- [C#](https://docs.microsoft.com/en-us/dotnet/csharp/) / [.NET 10](https://dotnet.microsoft.com/en-us/download) - Back-end language and runtime
- [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/overview?view=aspnetcore-10.0) - Web framework
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) - ORM for database access
- [MailKit](https://github.com/jstedfast/MailKit) - Email client library
- [SQLite](https://sqlite.org/docs.html) - Development database
- [PostgreSQL 18](https://www.postgresql.org/docs/18/index.html) - Production database

### Testing

- [xUnit](https://xunit.net/) - Test framework
- [NSubstitute](https://nsubstitute.github.io/) - Mocking library
- [Shouldly](https://docs.shouldly.org/) - Assertion library
- [Coverlet](https://github.com/coverlet-coverage/coverlet) - Code coverage

## Quickstart Guide

### Prerequisites

- [.NET 10.0](https://dotnet.microsoft.com/en-us/download) or later
- [Node.js v24](https://nodejs.org/en/download) (npm v11) or later

### 1 - Set up

Create a `.env` file in the root directory of the project, copy the format from `example.env` and fill out the details.

_The email and app password used are pinned in the Team300 devops mattermost channel._

For LiveKit configuration. Follow these steps:

1. Create a LiveKit account at <https://cloud.livekit.io/login>
2. Follow the prompts to create a new account and project
3. Once created, open the project, click on "Settings" in the left sidebar, and then click on "API Keys"
4. Click on "Create Key", create a new key then copy the Websocket URL, API Key and API Secret
5. Add the Websocket URL, API Key and API Secret to the `.env` file in the root directory of the project.

You can view your usage on the LiveKit dashboard.

### 2 - Install dependencies

From the `Grinder.Api` directory:

```bash
dotnet restore
```

From the `Grinder.App` directory:

```bash
npm install
```

### 3 - Run the project

From the `Grinder.Api` directory:

```bash
dotnet run
```

The REST API will be available on port 5000.

From the `Grinder.App` directory:

```bash
npm run dev
```

The app will be available at <http://localhost:5173>.

## Features

### User Authentication

- Registration with email, display name, password, and country
- Password strength enforcement (8+ characters, uppercase, lowercase, number, and special character)
- Email uniqueness validation
- Login with HTTP-only cookie authentication
- Email verification via verification codes
- Forgot password and password reset via email codes
- CSRF token protection

### User Profile

- View profile details (display name, profile picture, tasks, friends, status ring)
- Edit profile (email, display name, country, profile picture)
- Status Ring showing the ratio of a user's tasks in progress, todo, done, or overdue
- Grid of a user's tasks, filtered by visibility relative to the viewer
- Own profile shows all tasks regardless of visibility, with an edit control in place of relationship controls
- Switch between grid, calendar, and chronological task views on your own profile
- Friend count shown, linking to the friend list
- Badges page showing locked/unlocked badges with name, icon, and unlock criteria
- Select an unlocked badge (or none) to display on your public profile

### Task Management

- Create task lists to organise tasks into groups
- Create tasks with a required title, description, due date, status, and visibility, plus optional due time, start date/time, parent task, category, and up to 5 image/video attachments
- Edit existing tasks
- View all tasks grouped by their parent tasks, with subtasks nested under parents (max nesting depth of 3)
- View task details on a dedicated task page
- Task status with three states: Todo, In Progress, and Done, colour-coded
- Category autocomplete when creating a task, with the option to create a new category on the fly

### Task Attachments

- Attach up to 5 images or videos to a task via a file picker and confirmation modal
- Preview images and video thumbnails before uploading, with playback preview for videos
- Remove attachments from an existing task
- File type validation (images: png, jpg, svg, gif; video: mp4, mov, webm) and a 50MB size limit on videos
- A task with a single video attachment is automatically classified as a clip

### Date Entry

- Due dates and due time validated to be in the future
- Locale-aware manual entry (DD/MM/YYYY or MM/DD/YYYY based on browser locale)
- Start date and time are validated to be before due date and time
- Due time defaults to 11:59pm if not set, start time defaults to 12:00am if not set and start date is set

### Friends

- Search for friends on the Explore page, with result cards showing the current relationship status
- Follow, follow back, and become friends when both users follow each other
- Friend notifications page showing recent followers in reverse chronological order, with inline controls to change relationship status
- Combined pending-request count shown on the navigation bar

### Calendar

- View tasks on calendar located at the due time or spanning from the start time to due time if start time and date is set
- Filter tasks by status or category

### Public Tasks (feed)

- Scrollable feed of tasks visible to you, with friends' tasks shown first
- React to tasks with a predefined set of icons, with a running count per reaction
- Remove your own reaction
- Receive a notification when someone reacts to or comments on your task
- Edited tasks reappear in the feed as if new, with an "updated" indicator

### Comments

- Comment on tasks and clips, with support for replies
- Delete your own comments
- 2048-character limit, with the Post button disabled while empty
- Comments listed in reverse chronological order
- Reply to comments, soft delete on comment tree parent

### Clips

- Scrollable Clips page for browsing publicly posted video tasks, auto-playing on scroll
- View full description, poster's profile, react, and comment from within a clip
- Reacting shows a cool animation

### Explore Page

- Cold-start feed grouped by trending content (reactions + comments over the last 24h), unseen content first
- Search across tasks, clips, and users, grouped by match strength then interaction count
- Paginated results with no duplicates across pages, and an end-of-feed message
- Switch between All / Tasks / Clips / Users result views
- Terms of Service, Cookie Policy, and Attributions pages accessible from profile settings

### Direct Messaging

- Messages page listing conversations with a preview of the latest message and unread indicators
- Send and receive text messages (1–256 characters) in real time, with retry on failed sends
- Share a task or clip directly to one or more friends, delivered as a linked message preview
- Unread indicators clear automatically when a conversation is opened
- Real time message in-app notifications

### Live Streaming

- Start a stream from a connected task, with camera/microphone device selection and live preview
- Public streams cannot be linked to a Friends Only task
- Live view with camera preview, chat, viewer count, and an End Stream control, with a leave-page warning
- Toggle camera/audio on and off mid-stream
- Browse active streams (ordered by relationship, then follow status, then view count), with a live chat and real-time viewer count
- Ended streams show a "Stream Ended" state with a link back to the associated task

### Grinder Wrapped

- Year-in-review playback (task stats, most-liked task/comment, top categories, personal stats) for eligible accounts (2+ months old, at least one task completed this year)
- Tap to skip forward/replay, hold to pause; closing slide offers share and return-to-app options
- Previous year's Wrapped accessible from your profile if eligible

## Project Structure

```
team-300/
├── Grinder.Api/                     # ASP.NET Core back-end
│   ├── Controllers/                 # API endpoints
│   ├── Services/                    # Business logic
│   ├── Models/
│   │   ├── Attachments/
│   │   ├── DTOs/                    # Request/response objects
│   │   └── Entities/                # Database entities
│   ├── DataAccess/                  # EF Core database context and sample user data
│   ├── Migrations/                  # Migration context and files
│   ├── Utilities/                   # Helpers (session, validation)
│   └── Program.cs                   # App configuration and startup
├── Grinder.App/                     # SvelteKit front-end
│   └── src/
│       ├── routes/
│       │   ├── (protected)/         # Auth-required routes
|       |   |   ├── badges/          # A page for the different badges a user can earn
|       |   |   ├── chat/            # Direct messages page
|       |   |   ├── explore/         # Explore overview page
│       │   │   ├── feed/            # Scrollable feed page
│       │   │   ├── friends/         # Friends list page
│       │   │   ├── livestream/      # Page to start a live stream
│       │   │   ├── notifications/   # Notification management page
|       |   |   ├── profile/         # User profile page
│       │   │   ├── tasks/           # Task management page
│       │   │   ├── update-password/ # Password update page
│       │   │   └── watch/           # The view switcher for clips and streams
│       │   └── (public)/
│       │       ├── login/
│       │       ├── register/
│       │       ├── email-verification/
│       │       └── reset-password/
│       └── lib/
|           ├── api/                 # API client and endpoint bindings
│           ├── components/          # Reusable UI components
|           ├── requests/            # Typed request helpers
│           ├── stores/              # Svelte stores (auth state)
│           └── *.ts                 # Utilities (CSRF, dates, types)
├── Grinder.Api.Tests/               # Back-end test suite
│   ├── E2E/                         # End to End testing including test fixture and user story files
│   ├── Acceptance/
|   |    ├── Features/               # Gherkin feature file scenarios per user story
|   |    ├── Setup/                  # Shared test fixture for acceptance testing
|   |    ├── StepDefinitions/        # Step binding implementation to test gherkin steps
│   ├── Integration/
│   |   ├── Controllers/             # Controller integration tests
│   |   └── Services/                # Service integration tests
|   └── Unit/                        # Unit tests
└── runner-scripts/                  # CI/CD deployment scripts
```

## Running Tests

From the `Grinder.Api.Tests` directory:

```bash
dotnet test
```

With coverage:

```bash
dotnet test /p:CollectCoverage=true
```

Run a specific test category:

```bash
dotnet test --filter "FullyQualifiedName~Unit"
dotnet test --filter "FullyQualifiedName~Integration"
```

## Linting

From the `Grinder.App` directory:

```bash
npx prettier --check src/
```

### Migrations

To update the database generate a migration file by runnning the following commmand

From the `Grinder.Api` directory:

```bash
dotnet ef migrations add {MigrationFileName}
```

## Attributions & Licensing

Grinder is built on a number of open-source projects and icon sets. We're grateful to their authors and maintainers. The full, up-to-date list is also viewable in-app under Profile Settings → Attributions.

### Front-end Dependencies

| Project | Licence |
|---|---|
| [Svelte](https://svelte.dev) | MIT |
| [SvelteKit](https://kit.svelte.dev) | MIT |
| [TypeScript](https://www.typescriptlang.org) | Apache License 2.0 |
| [Vite](https://vite.dev) | MIT |
| [Tailwind CSS](https://tailwindcss.com) | MIT |
| [DayFlow](https://calendar.dayflow.studio) (core & Svelte bindings) | MIT |
| [@microsoft/signalr](https://learn.microsoft.com/aspnet/core/signalr) | MIT |
| [sveltednd](https://www.npmjs.com/package/@thisux/sveltednd) (drag and drop) | MIT |

### Back-end Dependencies

| Project | Licence |
|---|---|
| [.NET / ASP.NET Core](https://dotnet.microsoft.com) | MIT |
| [Entity Framework Core](https://learn.microsoft.com/ef/core) (incl. SQLite provider) | MIT |
| [Npgsql.EntityFrameworkCore.PostgreSQL](https://www.npgsql.org/efcore) | PostgreSQL Licence |
| [MailKit](https://github.com/jstedfast/MailKit) | MIT |
| [Swashbuckle (Swagger UI)](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) | MIT |
| [LiveKit](https://livekit.io) (live broadcast infrastructure) | Apache License 2.0 |

### Testing Dependencies

| Project | Licence |
|---|---|
| [xUnit](https://xunit.net) (incl. xunit.runner.visualstudio) | Apache License 2.0 |
| [Microsoft.Playwright](https://playwright.dev/dotnet/) | Apache License 2.0 |
| [Reqnroll](https://reqnroll.net) (incl. Reqnroll.xUnit) | BSD 3-Clause Licence |
| [NSubstitute](https://nsubstitute.github.io) | BSD 3-Clause Licence |
| [Shouldly](https://docs.shouldly.org) | BSD 3-Clause Licence |
| [coverlet](https://github.com/coverlet-coverage/coverlet) | MIT |

### Icons

Most icons throughout the app are drawn from or adapted from [Lucide](https://lucide.dev), distributed under the ISC Licence. A handful of additional icons were sourced from [SVG Repo](https://www.svgrepo.com), each under its own free-to-use licence (CC0, MIT, or public domain, depending on the individual icon).

### License texts

Full licence texts for the packages above are available from their respective project pages, or from the `node_modules` and NuGet package caches used to build Grinder.

## Acknowledgements

This project was created as a part of the University of Canterbury's SENG302 course. The course information can be found [here](https://courseinfo.canterbury.ac.nz/GetCourseDetails.aspx?course=SENG302)

