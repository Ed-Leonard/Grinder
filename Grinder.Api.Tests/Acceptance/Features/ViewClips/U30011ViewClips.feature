@U30011
Feature: U30011 - As Sage, I want to be able to view public clips that other users have posted so that I can doom scroll in a motivating way and see others engaging with tasks.

Background:
    Given I am logged in as a valid user

Scenario: AC1 - View Clips
    Given another user has posted a public clip
    When I view the clips page
    Then I see that clip in the feed
    And it shows the video attachment, the poster's profile info, and the description

# AC2 - View full description, frontend-only expand behaviour, cannot be tested

# AC3 - View profile, frontend-only navigation, cannot be tested

Scenario: AC4 - React to a clip
    Given another user has posted a public clip
    And a reaction "Fire" exists
    When I react to the clip with "Fire"
    Then the reaction is added to the clip
    And the reaction count is 1

Scenario: AC5 - Remove reaction to a clip
    Given another user has posted a public clip
    And a reaction "Fire" exists
    And I have reacted to the clip with "Fire"
    When I react to the clip with "Fire" again
    Then the reaction is removed from the clip
    And the reaction count is 0

Scenario: AC6 - Comment on a clip
    Given another user has posted a public clip
    When I post the comment "Great form!" on the clip
    Then the comment is added to the clip
    And I can see it when I view the clip's comments