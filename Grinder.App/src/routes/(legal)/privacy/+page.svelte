<script lang="ts">
  import LegalPage from "$lib/components/utils/LegalPage.svelte";
  import { resolve } from "$app/paths";
</script>

<LegalPage title="Privacy Policy" lastUpdated="17 September 2026">
  <!-- all written by claude--> 
  <p>
    This Privacy Policy explains what personal information Grinder collects,
    why, and how it's handled. It should be read alongside our <a
      href={resolve("/terms")}>Terms of Service</a
    >
    and <a href={resolve("/cookies")}>Cookie Policy</a>.
  </p>

  <h2>1. Who We Are</h2>
  <p>
    Grinder is a student software project built by a team as part of a
    university course at the University of Canterbury. It is not a company
    or a commercial service. For the purposes of the New Zealand Privacy Act
    2020, the project team acts as the agency responsible for personal
    information collected through the app. Course staff and markers may also
    access the system and its data for assessment purposes.
  </p>

  <h2>2. Information We Collect</h2>
  <p>Information you give us directly:</p>
  <ul>
    <li>Account details - email address, display name, country, and password (see <a href="#security">Security</a> for how passwords are stored);</li>
    <li>Profile information - an optional profile picture;</li>
    <li>
      Content you create - tasks, task lists, comments, reactions, badge
      progress, and photos or video clips you upload;
    </li>
    <li>
      Live audio/video - if you start or join a live broadcast, your
      microphone and/or camera stream is transmitted for the duration of
      that session;
    </li>
    <li>
      Social connections - who you follow or are friends with on Grinder;
    </li>
    <li>
      Communications - support or bug reports you send us directly.
    </li>
  </ul>
  <p>Information collected automatically:</p>
  <ul>
    <li>
      Standard web server logs (IP address, browser/device type, timestamps
      of requests) generated as part of running the app;
    </li>
    <li>
      Strictly necessary cookies used to keep you signed in and protect
      against cross-site request forgery - see our <a
        href={resolve("/cookies")}>Cookie Policy</a
      > for details. We don't use analytics or advertising cookies.
    </li>
  </ul>

  <h2>3. How We Use Your Information</h2>
  <p>We use the information above to:</p>
  <ul>
    <li>Create and operate your account, and authenticate your sign-ins;</li>
    <li>
      Show your tasks, clips, and activity to the friends and audiences you
      choose to share them with;
    </li>
    <li>
      Send transactional emails, such as email verification and
      password-reset codes;
    </li>
    <li>Provide live broadcast and real-time features;</li>
    <li>Maintain the security and integrity of the app; and</li>
    <li>Respond to support requests, and assess the project for coursework.</li>
  </ul>
  <p>
    We don't use your personal information for advertising, and we don't
    sell it to anyone.
  </p>

  <h2>4. How We Share Information</h2>
  <p>We don't sell personal information. It's only shared:</p>
  <ul>
    <li>
      <strong>Within Grinder</strong> - with the friends, followers, or
      audiences you choose to share tasks, clips, or broadcasts with, based
      on your own sharing choices in the app;
    </li>
    <li>
      <strong>With service providers</strong> we rely on to run the app:
      <ul>
        <li>
          <a
            href="https://livekit.io"
            target="_blank"
            rel="noopener noreferrer">LiveKit</a
          >, which provides the infrastructure for live audio/video
          broadcasts - your stream is routed through their servers while a
          broadcast is active;
        </li>
        <li>
          Google's Gmail service, used to send verification and
          password-reset emails via SMTP;
        </li>
      </ul>
    </li>
    <li>
      <strong>Course staff</strong>, where needed to assess or support the
      project; and
    </li>
    <li>
      <strong>If required by law</strong>, for example in response to a valid
      legal request.
    </li>
  </ul>

  <h2>5. Where Your Data Is Stored</h2>
  <p>
    Grinder's database and uploaded files (profile pictures, task
    attachments, clips) are hosted on infrastructure managed for the course
    project. Live broadcast media passes through LiveKit's infrastructure,
    and verification emails are relayed through Google's servers - both of
    which may process data outside New Zealand as part of providing their
    service.
  </p>

  <h2 id="security">6. Security</h2>
  <p>
    Passwords are never stored in plain text - they're hashed before being
    saved. Authentication and CSRF cookies are HTTP-only, and connections are
    encrypted (HTTPS) in staging and production. That said, no method of
    transmission or storage is completely secure, and we can't guarantee
    absolute security.
  </p>

  <h2>7. Data Retention</h2>
  <p>
    We keep your information for as long as your account is active. If you
    delete your account, your personal information and content are removed
    or de-identified within a reasonable time, though copies may briefly
    persist in backups until they naturally roll over.
  </p>

  <h2>8. Your Rights</h2>
  <p>
    Under the Privacy Act 2020, you have the right to ask for access to, and
    correction of, the personal information Grinder holds about you. You can
    update most of your details (email, display name, country, profile
    picture) directly from your profile settings, or contact us at <a
      href="mailto:privacy@grinder.co.nz">privacy@grinder.co.nz</a
    >
    for anything else, including account deletion.
  </p>

  <h2>9. Children</h2>
  <p>
    Grinder is not directed at children under 13, and we don't knowingly
    collect personal information from anyone under that age.
  </p>

  <h2>10. Changes to This Policy</h2>
  <p>
    We may update this Privacy Policy as the project evolves. The "Last
    updated" date at the top of this page reflects the most recent revision.
  </p>

  <h2>11. Contact &amp; Complaints</h2>
  <p>
    Questions about this policy, or requests relating to your personal
    information, can be sent to <a href="mailto:privacy@grinder.co.nz"
      >privacy@grinder.co.nz</a
    >. If you're not satisfied with our response, you can complain to the
    New Zealand
    <a
      href="https://www.privacy.org.nz"
      target="_blank"
      rel="noopener noreferrer">Office of the Privacy Commissioner</a
    >.
  </p>
</LegalPage>
