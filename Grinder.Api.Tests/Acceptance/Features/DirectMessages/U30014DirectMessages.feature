@U30019
Feature: U30019 - As Lei, I want to be able to send tasks and clips to my friends to show them new things to try out. I also want to directly communicate with my peers to discuss progress and enhance our ability to organise group work.

Background:
    Given I am logged in as a valid user
    And I am friends with someone

Scenario: AC3 - Sending a direct message
    Given I am viewing a conversation
    When I type a valid message between 1 and 256 characters
    And I click on the 'Send' button
    Then the message is sent to my friend
    And persisted in the chat history
    And shows up in the conversation as being sent

Scenario: AC4 - Receiving a direct message
    When they send me a direct message
    Then I am notified that I have received a message via the in-app notification bell indicator and notification page
    And there is an unread indicator on the Messages page
    When I open the direct message
    Then it shows up in the conversation without reloading the page
    And the unread indicator is cleared

Scenario: AC9 - Marking messages as read
    Given I have opened an unread conversation
    When I navigate back to the Direct Messages page
    Then that conversation no longer shows an unread indicator
