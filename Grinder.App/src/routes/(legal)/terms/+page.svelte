<script lang="ts">
  import LegalPage from "$lib/components/utils/LegalPage.svelte";
  import { resolve } from "$app/paths";
</script>

<LegalPage title="Terms of Service" lastUpdated="17 September 2026">
  <!-- all written by claude-->
  <p>
    Grinder ("<strong>Grinder</strong>", "<strong>we</strong>", "<strong
      >us</strong
    >") is a student software project built as part of a university course
    (team 300). It is not a commercial product, and these Terms exist to set
    clear expectations for anyone using it during development, testing, or
    demonstration. Grinder is not affiliated with, and has no connection to,
    any other product or company that shares a similar name.
  </p>
  <p>
    By creating an account or otherwise using Grinder, you agree to these
    Terms of Service, our <a href={resolve("/privacy")}>Privacy Policy</a>,
    our <a href={resolve("/cookies")}>Cookie Policy</a>, and any other
    policies referenced here. If you do not agree, please do not use the app.
  </p>

  <h2>1. Eligibility</h2>
  <p>
    You must be at least 13 years old to create a Grinder account. By
    registering, you confirm that the information you provide (email, display
    name, country) is accurate and that you meet this age requirement.
  </p>

  <h2>2. Your Account</h2>
  <p>
    You're responsible for keeping your login credentials confidential and
    for all activity that happens under your account. Tell us as soon as
    possible if you suspect unauthorised use of your account. We may suspend
    or terminate accounts that are inactive, abandoned, or created with false
    information.
  </p>

  <h2>3. Acceptable Use</h2>
  <p>When using Grinder, you agree not to:</p>
  <ul>
    <li>
      Upload, post, or broadcast content that is illegal, harassing, hateful,
      sexually explicit, or that infringes someone else's rights;
    </li>
    <li>
      Impersonate another person, or misrepresent your affiliation with
      anyone;
    </li>
    <li>
      Attempt to gain unauthorised access to other accounts, the API, or the
      underlying systems;
    </li>
    <li>
      Use the service to send spam, malware, or to disrupt other users'
      experience; or
    </li>
    <li>
      Scrape, reverse engineer, or misuse the app outside of normal,
      good-faith use as a task-tracking and social accountability tool.
    </li>
  </ul>
  <p>
    We may remove content or suspend accounts that breach these rules,
    without prior notice.
  </p>

  <h2>4. Your Content</h2>
  <p>
    Grinder lets you create tasks, task lists, comments, badges progress, and
    share photos, video clips, and live broadcasts with friends. You retain
    ownership of anything you create or upload ("<strong
      >User Content</strong
    >"). By posting User Content, you grant Grinder a limited, non-exclusive
    licence to store, display, and transmit it to the friends and audiences
    you choose to share it with, solely so the app can function as intended
    (for example, showing your clips in a friend's feed).
  </p>
  <p>
    You're solely responsible for what you post. Don't upload anything you
    don't have the right to share.
  </p>

  <h2>5. Third-Party Services</h2>
  <p>
    Live broadcasts and some real-time features are powered by LiveKit, a
    third-party video/audio infrastructure provider. When you go live or join
    a broadcast, your audio/video stream is routed through LiveKit's
    infrastructure under its own terms. We are not responsible for outages or
    issues originating from third-party providers we rely on.
  </p>

  <h2>6. Intellectual Property</h2>
  <p>
    The Grinder name, interface, and underlying code (excluding your User
    Content and the open-source components listed on our <a
      href={resolve("/attributions")}>Attributions</a
    > page) belong to the project's student contributors. This is a coursework
    project rather than a commercial release, and no promises are made about ongoing
    availability, support, or maintenance beyond the course.
  </p>

  <h2>7. Termination</h2>
  <p>
    You may stop using Grinder and request deletion of your account at any
    time. We may suspend or terminate access to the app, in whole or in part,
    at any time - including at the end of the course the project was built
    for.
  </p>

  <h2>8. Disclaimer of Warranties</h2>
  <p>
    Grinder is provided "as is" and "as available", as a student project,
    without warranties of any kind, express or implied, including fitness for
    a particular purpose, reliability, or uninterrupted availability. Features
    may change, break, or be removed without notice as development continues.
  </p>

  <h2>9. Limitation of Liability</h2>
  <p>
    To the maximum extent permitted by law, the project's contributors are
    not liable for any indirect, incidental, or consequential damages
    arising from your use of, or inability to use, Grinder, including loss of
    data, tasks, or content.
  </p>

  <h2>10. Changes to These Terms</h2>
  <p>
    We may update these Terms from time to time as the project evolves. The
    "Last updated" date at the top of this page reflects the most recent
    changes. Continuing to use Grinder after changes take effect means you
    accept the updated Terms.
  </p>

  <h2>11. Governing Law</h2>
  <p>
    These Terms are governed by the laws of New Zealand, without regard to
    conflict-of-law principles.
  </p>

  <h2>12. Contact</h2>
  <p>
    Questions about these Terms can be sent to <a
      href="mailto:privacy@grinder.co.nz">privacy@grinder.co.nz</a
    >.
  </p>
</LegalPage>
