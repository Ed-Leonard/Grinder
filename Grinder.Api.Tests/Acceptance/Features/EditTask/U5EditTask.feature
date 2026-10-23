@U5
Feature: U5 - As Lei I want to update task information So That I can add more details when my lecturers share more information about my assignments

Background:
    Given I am logged in as a valid user
    And I have a task item

# AC1 - Edit Task form, cannot be tested

Scenario: AC2 - Edit Task with all properties
    Given I enter a valid title, an optional description, an optional due date, an optional due time, and a status from the options 'Todo', 'In Progress', and 'Done' 
    When I click 'Update'
    Then the task is updated 

Scenario: AC3 - Edit Task with no description
    Given I leave the description empty 
    When I click 'Update'
    Then the Task description is set to empty

Scenario: AC4 - Edit Task with wrong size for title
    Given I enter a title that is not between 3 and 128 characters 
    When I click 'Update'
    Then I see an error message 'Title is required and must be between 3 and 128 characters long'

Scenario: AC5 - Edit Task long description
    Given I enter a description longer than 2048 characters
    When I click 'Update'
    Then I see an error message 'Description must be 2048 characters or less'

# AC6 - Edit Task with invalid due date, cannot be tested

Scenario: AC7 - Edit Task with due date in the past
    Given I enter a due date in the past 
    When I click 'Update'
    Then I see an error message 'Invalid due date, date must be in the future'

Scenario: AC8 - Cancel edition of task
    Given I click the cancel button 
    Then no changes are saved
