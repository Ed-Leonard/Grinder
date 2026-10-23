@U9
Feature: U9 - As Sifa I want to update my password So that I can keep my account secure when I change countries

Background:
    Given I am logged in as a valid user

Scenario: AC1 - Send password change code
    Given I click 'Update password'
    Then an email is sent to me with a unique code to update my password

Scenario: AC2 - Enter valid code
    Given I have received the change code by email
    When I enter a valid code corresponding to my change-password request
    Then the code is validated

Scenario: AC3 - Change password success
    Given I enter my old password that matches the password on file
    And I enter twice the same acceptable password
    When I click 'Update Password'
    Then the password has been updated successfully

Scenario: AC4 - Change password fails with wrong old password
    Given I enter an old password that does not match the password on file
    When I click 'Update Password'
    Then an error message tells me "Old password does not match password on file"

Scenario: AC5 - Change password fails with non-matching passwords
    Given I enter two new passwords that do not match
    When I click 'Update Password'
    Then an error message tells me "Passwords do not match"

Scenario: AC6 - Change password fails with weak password
    Given I enter a new password that does not contain at least 8 characters
    And contains at least one of each uppercase, lowercase, numbers and special characters
    When I click 'Update Password'
    Then an error message tells me "Password must be at least 8 characters long including at least one of each uppercase, lowercase, numbers and special characters"

Scenario: AC7 - Cancel password change
    Given I click 'Cancel'
    Then no changes are applied to my password