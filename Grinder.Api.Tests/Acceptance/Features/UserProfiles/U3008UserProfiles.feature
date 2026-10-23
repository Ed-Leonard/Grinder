@U3008
Feature: U3008 - As Indiana I want to view my own and my friends' profiles so that I can see what adventures we are carrying out and track our progress.

Background:
    Given I am logged in as a valid user

# AC1 - Navigating to a profile is a client-side routing action with no distinct API contract,
# so it cannot be tested at this layer. Covered from the friends list by the browser E2E test
# ShouldNavigateToAFriendsProfileFromTheFriendsList in E2E/ProfileViewSwitcherE2ETest.cs.

Scenario: AC2 - Content shown on a profile
    Given I have a task
    When I view my own profile
    Then I see my display name
    And I see my friend count
    And the task grid contains my task

# AC2 - The profile picture (or default avatar when none is set) is not included in the
# profile API responses, so it cannot be asserted at this layer.

Scenario Outline: AC3 - Relationship status is shown when viewing another user's profile
    Given another user exists
    And our relationship is "<relationship>"
    When I view their profile
    Then I see the relationship status "<relationship>"

Examples:
    | relationship |
    | Follow       |
    | Following    |
    | FollowBack   |
    | Friends      |

Scenario: AC3 - Task grid shows only public tasks when we are not friends
    Given another user exists
    And they have a public task, a friends-only task, and a private task
    When I view their task grid
    Then I only see their public task

Scenario: AC3 - Task grid shows public and friends-only tasks when we are friends
    Given another user exists
    And they have a public task, a friends-only task, and a private task
    And we are friends
    When I view their task grid
    Then I see their public task and their friends-only task
    And I do not see their private task

Scenario: AC4 - My own task grid shows all of my tasks regardless of visibility
    Given I have a public task, a friends-only task, and a private task
    When I view my own task grid
    Then I see all 3 of my tasks

# AC4 - The Edit Profile control appearing in place of relationship controls is a UI-only
# concern with no corresponding API contract, so it cannot be tested at this layer.

# AC5 - Switching to calendar view is a client-side UI concern with no corresponding API
# contract, so it cannot be tested at this layer. Covered by the browser E2E test
# ShouldSwitchToCalendarViewOnOwnProfile in E2E/ProfileViewSwitcherE2ETest.cs.

Scenario: AC6 - My tasks are ordered by closest due date first
    Given I have tasks due on "2026-09-01", "2026-08-25", and "2026-09-10"
    When I view my own task grid
    Then my tasks are returned ordered by closest due date first

Scenario: AC7 - Progress ring reflects the status of visible tasks
    Given I have one todo task, one in progress task, one done task, and one overdue task
    When I view my own progress status counts
    Then the status counts show 1 todo, 1 in progress, 1 done, and 1 overdue

Scenario: AC7 - Progress ring is in an empty state when there are no visible tasks
    When I view my own progress status counts
    Then the status counts show 0 todo, 0 in progress, 0 done, and 0 overdue

Scenario: AC8 - View all badges with locked and unlocked visually distinguished
    Given I have earned a badge
    When I navigate to the Badges page
    Then I see all badges returned
    And each badge has a name, icon, and unlock criteria
    And unlocked badges are visually distinguished from locked badges
    And unlocked badges show when they were earned

Scenario: AC8 - View badges when no badges have been earned
    When I navigate to the Badges page
    Then I see all badges returned
    And all badges are shown as locked
    And no badges have an earned date

Scenario: AC8 - Each badge shows its name, icon, and unlock criteria
    When I navigate to the Badges page
    Then I see all badges returned
    And each badge has a name, icon, and unlock criteria

Scenario: AC9 - Only my unlocked badges are offered for selection
    Given I have earned the badge "The Grinder"
    When I request my badges for selection
    Then "The Grinder" is offered as selectable
    And "Flawless" is not offered as selectable

# AC9 - The "No Status" option in the badge dropdown is a client-side affordance representing
# no badge selected; it has no corresponding entry in the API response, so it cannot be
# asserted at this layer.

Scenario: AC10 - Selecting an unlocked badge saves it to my public profile
    Given I have earned the badge "The Grinder"
    When I select the badge "The Grinder"
    Then the selection succeeds
    And my public profile shows the badge "The Grinder"

Scenario: AC10 - Selecting a badge I have not earned is rejected
    Given the badge "Flawless" exists and I have not earned it
    When I select the badge "Flawless"
    Then the selection is rejected
    And my public profile shows no badge

Scenario: AC10 - Selecting No Status removes my current badge
    Given I have earned the badge "The Grinder"
    And I have selected the badge "The Grinder"
    When I select "No Status"
    Then the selection succeeds
    And my public profile shows no badge
