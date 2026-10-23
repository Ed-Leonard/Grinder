@U4
Feature: U4 - As Lei I want to create a new Task So that I can track tasks to complete my assignments.

Background:
    Given I am logged in as a valid user
    And I have entered valid task details for all fields

# AC1 -  Create a task in a task list, cannot be tested as functionality was removed

Scenario: AC2 - Create Task with all properties
    When I click "Create Task"
    Then the task is persisted 

Scenario: AC3 - Create task with no description
    And I enter no description
    When I click "Create Task"
    Then the task is persisted

Scenario Outline: AC4 - Create task with invalid title
    And I enter a title that is "<reason>"
    When I click "Create Task"
    Then I receive an error message

    Examples:
    | reason                    |
    | less than 3 characters    |
    | more than 128 characters  |

Scenario: AC5 - Create task with long description
    And I enter a description that is too long
    When I click "Create Task"
    Then I receive an error message

# AC6 - Create Task with invalid due date, cannot be tested

Scenario: AC7 - Create task with due date that is in the past
    And I enter a due date that is in the past
    When I click "Create Task"
    Then I receive an error message

Scenario: AC8 - Viewing a created task
    When I click on a task
    Then I can see its title, full description, due date, due time, status, and creation timestamp

# AC9 - Close task details, cannot be tested

# AC10 - Cancel creation of task, cannot be tested
