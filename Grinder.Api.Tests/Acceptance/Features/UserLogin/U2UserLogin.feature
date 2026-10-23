@U2
Feature: U2 - As Sarah I want to log in with my email and password So that I can access my task lists on Grinder.

Background:
    Given I have registered a new user account

Scenario: AC1 - User login with valid details
    Given I enter the email and password matching a registered account in the system
    When I click the 'Login' button
    Then I am authenticated successfully

Scenario Outline: AC2 - User login with invalid email
    Given I enter an email with an invalid format: "<email>"
    When I click the 'Login' button
    Then I see the an error message saying 'Invalid email address. Email must be in the format 'jane@doe.nz''

Examples:
    | email               |
    | test                |
    | test.com            |
    | @test.com           |
    | test@               |
    | test@test           |
    | test @test.com      |
    | test@test,com       |
    | test@.com           |

Scenario: AC3 - User login with a password that does not match the associated email in the system
    Given I enter an email that is registered to the system 
    And a password that does not match this email 
    When I click the 'Login' button 
    Then I see an error message 'Invalid email or password'

Scenario: AC4 - User login with an email that is not registered in the system
    Given I enter an email that is not registered in the system 
    When I click the 'Login' button 
    Then I see an error message 'Invalid email or password'

# AC5 - Cancel login, cannot be tested

# AC6 -  View user profile information when logged in, cannot be tested
