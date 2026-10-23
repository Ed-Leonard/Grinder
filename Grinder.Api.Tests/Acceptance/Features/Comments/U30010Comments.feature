@U30010
Feature: U30010 - As Indiana, I want to be able to comment on task to interact with my fellow explorers.

Background:
    Given I am logged in as a valid user
    And I am viewing a task with comments enabled
    
Scenario: AC1 - Comment on a task
    When I post the comment "Great work! 😀"
    Then the comment is added to the task with its text, timestamp, and author


Scenario: AC2 - Delete a comment
    Given I have posted the comment "Comment to delete"
    When I delete that comment
    Then the comment is removed from the task

Scenario: AC3 - Comment too long 
    Given I enter a comment longer than 2048 characters long
    When I click 'Post'
    Then I see an error message 'Comment must be 2048 characters or less'

# AC4 - empty comment, can't be tested (post button greyed out and disabled)

# AC5 - Reply to comment
Scenario: AC5 - Reply to a comment
    Given there is a parent comment on the task
    When I reply to that comment with "Thanks for the feedback!"
    Then the reply is added to the task and persisted
    And the reply is linked to the parent comment

Scenario: AC5 - Reply appears when fetching replies for a comment
    Given there is a parent comment on the task
    And I have replied to that comment with "First reply"
    When I fetch the replies for that comment
    Then the reply "First reply" is returned in the replies list

Scenario: AC5 - Reply count is incremented on parent comment
    Given there is a parent comment on the task
    And I have replied to that comment with "Reply one"
    And I have replied to that comment with "Reply two"
    When I view the task comments
    Then the parent comment has a reply count of 2

Scenario: AC6 - View comments in reverse chronological order
    Given the task has comments created at different times
    When I view the task comments
    Then the comments are shown in reverse chronological order