@U3001
Feature: U3001 - As Jane I want to be able to break up my ambitious tasks for world domination into smaller, smarter sub tasks that will allow me to properly track my progress by managing more specific goals.

Background:
    Given I am logged in as a valid user

Scenario: AC1 - Create task
    Given I have entered a valid task title, due date, and any other optional fields
    When I click the "Create Task" button
    Then the task is created
    And is displayed on the dashboard in the tasks

# AC2 - Create task page, cannot be tested

Scenario Outline: AC3 - Set task visibility
    Given I am on the create task form
    When I set the visibility to "<visibility>"
    Then the visibility of the task is set accordingly

Examples:
    | visibility    |
    | Public        |
    | FriendsOnly   |
    | Private       |

Scenario: AC4 - Create task with no due time
    Given I have not entered a due time
    When I create the task
    Then it shows up as having a due time of 11:59pm on the due date that was set

Scenario: AC5 - Create task with no start time
    Given I have entered a start Date
    And have not entered a start time
    Then it shows as having a start time of 12:00am on the start date that was set

Scenario: AC6 - Create task that is deeply nested child
    Given I am on the Create Task page
    And there is a task with 2 or more parents
    When I attempt to choose a parent task
    Then it does not show up in the drop down as an option for a parent

Scenario: AC7 - View tasks
    Given I am viewing my home page
    When I look at my tasks
    Then I see all my tasks in chronological order
    And any subtasks are clearly nested inside their parent task

# AC8 - Task page, cannot be tested

# AC9 - Choosing existing Category, cannot be tested

Scenario: AC10 - Creating Category
    Given I am creating a task
    And I enter a category that does not match any existing category
    When I create the task
    Then the new category is added to the task
    And I can select this category when creating more tasks