<script lang="ts">
  import LegalPage from "$lib/components/utils/LegalPage.svelte";
  import { resolve } from "$app/paths";
</script>

<LegalPage title="Cookie Policy" lastUpdated="17 September 2026">
  <!-- all written by claude-->
  <p>
    This policy explains how Grinder uses cookies and similar storage
    technologies. It should be read alongside our <a
      href={resolve("/terms")}>Terms of Service</a
    >
    and <a href={resolve("/privacy")}>Privacy Policy</a>.
  </p>

  <h2>1. What Are Cookies?</h2>
  <p>
    Cookies are small pieces of data stored on your device by your browser
    when you visit a website. They let a site recognise your browser between
    requests - for example, to keep you signed in.
  </p>

  <h2>2. Cookies We Use</h2>
  <p>
    Grinder only uses <strong>strictly necessary</strong> cookies required to
    make the app function. We do not use analytics, advertising, or
    third-party tracking cookies.
  </p>
  <table>
    <thead>
      <tr>
        <th>Cookie</th>
        <th>Purpose</th>
        <th>Duration</th>
        <th>Type</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td>Authentication cookie</td>
        <td>
          Keeps you signed in and identifies your account on subsequent
          requests. Stored as HTTP-only so it cannot be read by page
          scripts.
        </td>
        <td>7 days, refreshed while you're active</td>
        <td>Strictly necessary</td>
      </tr>
      <tr>
        <td>Session cookie</td>
        <td>
          Holds short-lived, server-side state during multi-step flows such
          as email verification and password resets.
        </td>
        <td>10 minutes of inactivity</td>
        <td>Strictly necessary</td>
      </tr>
      <tr>
        <td>CSRF token cookie</td>
        <td>
          Works alongside a request header to protect you from
          cross-site request forgery attacks on actions like logging out or
          updating your profile.
        </td>
        <td>Session (cleared when the browser closes)</td>
        <td>Strictly necessary</td>
      </tr>
    </tbody>
  </table>
  <p>
    All of the above cookies are set directly by Grinder (not by a
    third-party ad or analytics network), are marked HTTP-only so they can't
    be accessed by JavaScript, and are only sent over HTTPS in staging and
    production.
  </p>

  <h2>3. Cookies We Don't Use</h2>
  <p>
    We don't run any analytics, advertising, or cross-site tracking scripts,
    so Grinder doesn't set cookies for those purposes. If that changes, this
    page will be updated first.
  </p>

  <h2>4. Managing Cookies</h2>
  <p>
    Because the cookies above are strictly necessary, there's no separate
    consent banner for them - using the app requires them. You can still
    block or delete cookies through your browser settings, but doing so will
    prevent you from staying signed in or completing flows like email
    verification.
  </p>

  <h2>5. Changes to This Policy</h2>
  <p>
    We may update this Cookie Policy if the cookies Grinder uses change. The
    "Last updated" date at the top of this page reflects the most recent
    revision.
  </p>

  <h2>6. Contact</h2>
  <p>
    Questions about this policy can be sent to <a
      href="mailto:privacy@grinder.co.nz">privacy@grinder.co.nz</a
    >.
  </p>
</LegalPage>
