@U3002
Feature: U3002 - As Indiana, I want to upload images and videos of my adventures to tasks so that I can share them with others and refer to them later for further research.

Background:
    Given I am logged in as a valid user

# AC1 Open a file picker from create task, can't be tested
# AC2 Choose an attachment, can't be tested

Scenario: AC3 - Confirm and upload
    Given I have chosen a video
    And I am on the confirm attachments modal with a preview of the video
    When I click the 'Upload' button
    Then my video is uploaded to my task

# AC4 Add more attachments, can't be tested

Scenario: AC5 - Video file too large
    Given I choose a video that is more than 50MB in size
    When I confirm and click 'Upload'
    Then I see an error message saying 'Video is too large, please choose a smaller file'
    And my video is not uploaded

# AC6 Cancel from the file picker, can't be tested 

# AC7 can not be tested

Scenario: AC8 - Attempt to upload too many attachments
    Given the combined total of existing attachments and attachments I am attempting to upload is greater than 5
    When I click 'Upload'
    Then I see an error message saying 'Maximum of 5 attachments allowed'

Scenario: AC9 - Remove attachments
    Given I am on the edit task page
    When I click the x icon on an attachment
    And I click 'Update Task'
    Then the attachment is removed from the task
    And the change is persisted.

# AC10 Preview video in attachments modal, can't be tested

Scenario: AC11 - Upload a video as a reel
    Given I have uploaded a single video to a task
    When I create the task
    Then it is automatically classified as a clip

Scenario: AC12 - Unsupported file type for video
    Given I have selected a video file that is not of type mp4, mov, or webm
    When I confirm the selection
    Then I see the error message "The video must be of type mp4, mov, or webm."

Scenario: AC13 - Unsupported file type for image
    Given I have selected an image that is not of type png, jpg, svg, or gif
    When I confirm the selection
    Then I see the error message "The image must be of type png, jpg, svg, or gif"