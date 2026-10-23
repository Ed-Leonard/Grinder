Feature: U3009 - As Sage, I want to be able to view a public feed showing other users' public tasks, so that I can get inspiration from other people for tasks that could improve my wellness lifestyle.

Background:
    Given I am logged in as a valid user
    And I have a task

@U3009
Scenario: AC2 - React to a public task
    Given I am viewing a task
    When I click on the 'Face' icon
    Then I can React to that task with an icon selected from a predefined set of icons

@U3009
Scenario: AC2.1 - React to a public task
    Given I am viewing a task
    When I select a reaction
    Then the reaction icon is shown on the task with an updated count of the number of same reactions used shown next to it

@U3009
Scenario: AC3 - Remove reaction from a Public Task
    Given I am viewing a task that I have reacted to
    When I click the highlighted icon of the reaction button
    Then the highlight on the reaction button is removed
    And the number of reactions on that public task is decremented by 1
