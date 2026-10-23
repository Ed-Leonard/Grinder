@U6
Feature: U6 - As a Sifa I want to edit my profile information So that I can keep my account details up to date.
    
Background:
    Given I am logged in as a valid user

# AC1 -  Edit profile button on view user profile page, cannot be tested

Scenario: AC2 - Edit profile with valid data
    Given I have entered a valid email, display name, and select a country
    When I click "Update"
    Then I receive a confirmation message 
    And my changes are persisted

Scenario Outline: AC3 - Edit profile fails with malformed email address
    Given I have entered an malformed email address "<email>"
    When I click "Update"
    Then I receive an error message
    And no changes are applied to my profile

Examples:
    | email               |
    |                     |
    | test                |
    | test.com            |
    | @test.com           |
    | test@               |
    | test@test           |
    | test @test.com      |
    | test@test,com       |
    | test@.com           |

Scenario: AC4 - Edit profile fails with duplicate email address
    Given there is an account registered with the email "duplicate@email.com"
    And I enter the email "duplicate@email.com" the edit profile form
    When I click "Update"
    Then I receive an error message
    And no changes are applied to my profile

Scenario Outline: AC5 - Edit profile fails with invalid display name length
    Given I enter the display name "<name>" on the edit profile form
    When I click "Update"
    Then I receive an error message
    And no changes are applied to my profile

Examples:
    | name                  |
    |                       |
    | tu                    |
    | Lorem ipsum dolor sit amets consectetuer adipiscing elite Aenean commodo ligula eget dolors Aenean massaa Cum sociis natoque pent      |

Scenario Outline: AC6 - Edit profile fails with invalid display name characters
    Given I enter the display name "<name>" on the edit profile form
    When I click "Update"
    Then I receive an error message 
    And no changes are applied to my profile

Examples:
    | name                  |
    | displ4y               |
    | !name                 |
    | !@#$%^&*(){}          |
    | display#              |
    | display^              |
    | display.              |
    | display,              |

# AC7 - Cancel edit profile, cannot be tested