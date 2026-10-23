<script lang="ts">
  import LegalPage from "$lib/components/utils/LegalPage.svelte";
</script>

<LegalPage title="Attributions" lastUpdated="17 September 2026">
  <!-- all written by claude-->
  <p>
    Grinder is built on top of a number of open-source projects and icon
    sets. We're grateful to their authors and maintainers. This page lists
    the third-party software and assets used, along with their licences.
  </p>

  <h2>Frontend</h2>
  <table>
    <thead>
      <tr>
        <th>Project</th>
        <th>Licence</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td><a href="https://svelte.dev" target="_blank" rel="noopener noreferrer">Svelte</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://kit.svelte.dev" target="_blank" rel="noopener noreferrer">SvelteKit</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://www.typescriptlang.org" target="_blank" rel="noopener noreferrer">TypeScript</a></td>
        <td>Apache License 2.0</td>
      </tr>
      <tr>
        <td><a href="https://vite.dev" target="_blank" rel="noopener noreferrer">Vite</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://tailwindcss.com" target="_blank" rel="noopener noreferrer">Tailwind CSS</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://calendar.dayflow.studio" target="_blank" rel="noopener noreferrer">DayFlow</a> (core &amp; Svelte bindings)</td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://learn.microsoft.com/aspnet/core/signalr" target="_blank" rel="noopener noreferrer">@microsoft/signalr</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://www.npmjs.com/package/@thisux/sveltednd" target="_blank" rel="noopener noreferrer">sveltednd</a> (drag and drop)</td>
        <td>MIT</td>
      </tr>
    </tbody>
  </table>

  <h2>Backend</h2>
  <table>
    <thead>
      <tr>
        <th>Project</th>
        <th>Licence</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td><a href="https://dotnet.microsoft.com" target="_blank" rel="noopener noreferrer">.NET / ASP.NET Core</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://learn.microsoft.com/ef/core" target="_blank" rel="noopener noreferrer">Entity Framework Core</a> (incl. SQLite provider)</td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://www.npgsql.org/efcore" target="_blank" rel="noopener noreferrer">Npgsql.EntityFrameworkCore.PostgreSQL</a></td>
        <td>PostgreSQL Licence</td>
      </tr>
      <tr>
        <td><a href="https://github.com/jstedfast/MailKit" target="_blank" rel="noopener noreferrer">MailKit</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://github.com/domaindrivendev/Swashbuckle.AspNetCore" target="_blank" rel="noopener noreferrer">Swashbuckle (Swagger UI)</a></td>
        <td>MIT</td>
      </tr>
      <tr>
        <td><a href="https://livekit.io" target="_blank" rel="noopener noreferrer">LiveKit</a> (live broadcast infrastructure)</td>
        <td>Apache License 2.0</td>
      </tr>
    </tbody>
  </table>

    <h2>Testing</h2>
  <table>
    <thead>
      <tr>
        <th>Project</th>
        <th>Licence</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td><a href="https://xunit.net" target="_blank" rel="noopener noreferrer">xUnit</a> (incl. xunit.runner.visualstudio)</td>
        <td>Apache License 2.0</td>
      </tr>
      <tr>
        <td><a href="https://playwright.dev/dotnet/" target="_blank" rel="noopener noreferrer">Microsoft.Playwright</a></td>
        <td>Apache License 2.0</td>
      </tr>
      <tr>
        <td><a href="https://reqnroll.net" target="_blank" rel="noopener noreferrer">Reqnroll</a> (incl. Reqnroll.xUnit)</td>
        <td>BSD 3-Clause Licence</td>
      </tr>
      <tr>
        <td><a href="https://nsubstitute.github.io" target="_blank" rel="noopener noreferrer">NSubstitute</a></td>
        <td>BSD 3-Clause Licence</td>
      </tr>
      <tr>
        <td><a href="https://docs.shouldly.org" target="_blank" rel="noopener noreferrer">Shouldly</a></td>
        <td>BSD 3-Clause Licence</td>
      </tr>
      <tr>
        <td><a href="https://github.com/coverlet-coverage/coverlet" target="_blank" rel="noopener noreferrer">coverlet</a></td>
        <td>MIT</td>
      </tr>
    </tbody>
  </table>

  <h2>Icons</h2>
  <p>
    Most icons used throughout the app are drawn from or adapted from <a
      href="https://lucide.dev"
      target="_blank"
      rel="noopener noreferrer">Lucide</a
    >, distributed under the ISC Licence. A handful of additional icons were
    sourced from <a
      href="https://www.svgrepo.com"
      target="_blank"
      rel="noopener noreferrer">SVG Repo</a
    >, each under its own free-to-use licence (CC0, MIT, or public domain,
    depending on the individual icon).
  </p>

  <h2>License texts</h2>
  <p>
    Full licence texts for the packages above are available from their
    respective project pages linked here, or from the <code
      >node_modules</code
    > and NuGet package caches used to build Grinder.
  </p>
</LegalPage>
