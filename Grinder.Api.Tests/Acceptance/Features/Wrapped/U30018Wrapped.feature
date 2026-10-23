@U30018
Feature: U30018 - As Lei, I want to be able to reflect on my year that I had with Grinder and see how productive I was through helpful statistics.

Background:
    Given I am logged in as a valid user

Scenario: AC1 - Eligible user can access current year's Wrapped
    Given my account is more than 2 months old
    And I have completed at least one task in the current calendar year
    When I check Wrapped eligibility
    Then I am eligible to view the current year's Wrapped

Scenario: AC2 - Eligible for previous year's Wrapped button
    Given I completed a task in 2025
    When I check Wrapped eligibility
    Then I am eligible to view the previous year's Wrapped

Scenario: AC2 - Not eligible for previous year's Wrapped button
    Given I have not completed a task in the previous calendar year
    When I check Wrapped eligibility
    Then I am not eligible to view the previous year's Wrapped

Scenario: AC3 - Viewing Wrapped Stats
    Given I am eligible for Wrapped
    And I have Wrapped slide data
    When I request my Wrapped stats
    Then I see tasks started and completed with a completion percent
    And I see my most-liked task and most-liked comment
    And I see my top 5 task categories

Scenario: AC5 - Account too new is ineligible for current year
    Given my account is less than 2 months old
    And I have completed at least one task in the current calendar year
    When I check Wrapped eligibility
    Then I am not eligible to view the current year's Wrapped

Scenario: AC5 - No completed task this year is not eligible
    Given my account is more than 2 months old
    And I have not completed a task in the current calendar year
    When I check Wrapped eligibility
    Then I am not eligible to view the current year's Wrapped