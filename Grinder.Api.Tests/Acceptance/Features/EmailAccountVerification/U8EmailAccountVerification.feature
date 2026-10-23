@U8
Feature: U8 - As Tipene I want to verify my email address when registering So that I can make sure I got my email address right.

Scenario: AC1 - A one-time code is issued on registration
    Given I enter a valid email, display name, an acceptable password, a repeated password matching the other password, and select a country from a list of options
    When I click "Register"
    Then a one-time verification code is stored for my account

Scenario: AC2 - Resending the one-time code invalidates the previous one
    Given I have registered a new account with email "axel@test.com"
    And a one-time code "676767" has been issued for "axel@test.com"
    When I request a new one-time code for "axel@test.com"
    Then a different one-time code is stored for "axel@test.com"
    And submitting the code "676767" for "axel@test.com" is rejected with "Invalid code"

Scenario: AC3 - A valid one-time code confirms registration
    Given I have registered a new account with email "axel@test.com"
    And a one-time code "676767" has been issued for "axel@test.com"
    When I submit the code "676767" for "axel@test.com"
    Then my account is marked as verified
    And I can log in successfully

Scenario: AC4 - An invalid one-time code is rejected
    Given I have registered a new account with email "axel@test.com"
    And a one-time code "676767" has been issued for "axel@test.com"
    When I submit the code "999999" for "axel@test.com"
    Then I receive the error message "Invalid code"

Scenario: AC5 - The one-time code expires after 5 minutes
    Given I have registered a new account with email "axel@test.com"
    And a one-time code "676767" has been issued for "axel@test.com"
    And 5 minutes have elapsed since the code was issued
    When I submit the code "676767" for "axel@test.com"
    Then I receive the error message "Code is no longer valid, account no longer exists"

Scenario: AC6 - Login is rejected for an unconfirmed account
    Given I have registered a new account with email "axel@test.com" and password "Password123!"
    And I have not yet confirmed my email
    When I attempt to log in as "axel@test.com" with password "Password123!"
    Then I receive the error message "Account is not validated yet, check your emails."
