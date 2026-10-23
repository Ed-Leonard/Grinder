@U1
Feature: U1 - As Sarah I want to register an account with my basic information So that I can start creating tasks with ‘Grinder’.

# AC1 - cannot be tested

Scenario: AC2 - User register a new account
    Given I enter a valid email, display name, an acceptable password, a repeated password matching the other password, and select a country from a list of options
    When I click "Register"
    Then I see a confirmation message
    And my login data is securely persisted

Scenario Outline: AC3 - Registration is rejected when the email address is malformed
    Given I enter the email "<email>" on the register form
    When I click "Register"
    Then I receive an error message

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

Scenario: AC4 - Registration is rejected when the email address is already in use
    Given there is an account registered with the email "test@test.com"
    And I enter the email "test@test.com" on the register form
    When I click "Register"
    Then I receive an error message

# AC5 - cannot be tested

Scenario: AC6 - Registration is rejected with an invalid password
    Given I enter the password "<password>" on the register form
    When I click "Register"
    Then I receive an error message

Examples:
    | password              |
    | alllowercase          |
    | alllowercase1         |
    | alllowercase!1        |
    | alllowercase!         |
    | ALLUPPERCASE          |
    | ALLUPPERCASE!         |
    | ALLUPPERCASE!2        |
    | sh0RT!                |
    | 12345678              |

Scenario Outline: AC7 - Registration is rejected with an invalid length of display name
    Given I enter the display name "<name>" on the register form
    When I click "Register"
    Then I receive an error message

Examples:
    | name                  |
    |                       |
    | tu                    |
    | Lorem ipsum dolor sit amets consectetuer adipiscing elite Aenean commodo ligula eget dolors Aenean massaa Cum sociis natoque pent      |

Scenario Outline: AC8 - Registration is rejected with display name including of invalid characters
    Given I enter the display name "<name>" on the register form
    When I click "Register"
    Then I receive an error message 

Examples:
    | name                  |
    | displ4y               |
    | !name                 |
    | !@#$%^&*(){}          |
    | display#              |
    | display^              |
    | display.              |
    | display,              |

# AC9 - cannot be tested