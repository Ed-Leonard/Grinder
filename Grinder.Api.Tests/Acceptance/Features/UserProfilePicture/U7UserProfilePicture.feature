@U7
Feature: U7 - As Kaia I want to upload a profile picture of myself so that my tenants can tell it's me at a glance

Background:
    Given I am logged in as a valid user

# AC1 -  Open file picker from user profile page, cannot be tested

Scenario Outline: AC2 - Update profile image with a valid image type
    Given I have selected a valid image of type "<contentType>" with extension "<extension>"
    When I submit the profile picture
    Then my profile picture is persisted

    Examples:
    | contentType   | extension |
    | image/jpeg    | .jpeg     |
    | image/png     | .png      |
    | image/svg+xml | .svg      |
    | image/gif     | .gif      |

Scenario Outline: AC3 - Invalid image types are rejected
    Given I have selected an image with content type "<contentType>" and extension "<extension>"
    When I submit the profile picture
    Then I receive an error message "The image must be of type png, jpg, svg, or gif"

    Examples:
    | contentType     | extension |
    | application/pdf | .pdf      |
    | text/plain      | .txt      |
    | image/bmp       | .bmp      |
    | image/webp      | .webp     |

Scenario: AC4 - Image larger than 5MB is rejected
    Given I have selected a valid image that is larger than 5MB
    When I submit the profile picture
    Then I receive an error message "Image is too large, please choose a smaller file"

# AC5 - Cancel profile picture from file picker, cannot be tested

# AC6 - Cancel profile picture from file cropping, cannot be tested