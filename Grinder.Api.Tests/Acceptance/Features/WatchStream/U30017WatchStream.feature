@U30017
Feature: U30017 - As John, I want to be able to watch a live stream so I can interact with my friends and people I am following in real time, and see how they progress with their tasks.

Background:
    Given I am logged in as a valid user

Scenario: AC1 - Public stream is visible to anyone
    Given another user is streaming publicly
    When I view the live streams list
    Then I see that stream in the list

Scenario: AC1 - Friends Only stream is hidden from a non-friend
    Given another user is streaming to friends only
    When I view the live streams list
    Then I do not see that stream in the list

Scenario: AC1 - Friends Only stream is visible to a friend
    Given another user is streaming to friends only
    And I am friends with that user
    When I view the live streams list
    Then I see that stream in the list

Scenario: AC2 - Friends' streams rank before followed and other public streams, regardless of viewer count
    Given a friend named "Freya" is streaming publicly with 1 viewer
    And a user I follow named "Devon" is streaming publicly with 100 viewers
    And a stranger named "Tally" is streaming publicly with 50 viewers
    When I view the live streams list
    Then the streams appear in this order:
        | name  |
        | Freya |
        | Devon |
        | Tally |

Scenario: AC2 - Streams within the same group are ordered by viewer count descending
    Given a stranger named "Quiet" is streaming publicly with 3 viewers
    And a stranger named "Popular" is streaming publicly with 10 viewers
    When I view the live streams list
    Then the streams appear in this order:
        | name    |
        | Popular |
        | Quiet   |

Scenario: AC2 - Ties on viewer count are broken by most recently started
    Given a stranger named "Early" is streaming publicly with 5 viewers
    And some time passes
    And a stranger named "Late" is streaming publicly with 5 viewers
    When I view the live streams list
    Then the streams appear in this order:
        | name  |
        | Late  |
        | Early |

Scenario: AC3 Open chat
    Given I am viewing a stream
    When I click on the chat icon
    Then the chat opens
    And I can see chat messages that are being sent at that time.

Scenario: AC4 Send message
    Given I am watching a stream
    And I have that stream's chat open
    When I send a message in the chat between 1 and 256 characters
    Then the message is displayed to all viewers
    And the streamer can see it in real time.

# Can't test AC6 because it is all client side 