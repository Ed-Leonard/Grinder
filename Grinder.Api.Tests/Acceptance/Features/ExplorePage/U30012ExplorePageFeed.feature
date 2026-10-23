@U30012
Feature: U30012 - As Indiana Jones, I want to be able to explore a feed where I can see content (Tasks and Clips) that relates to my search query, so that I can search for content relating to my current adventures.

Background:
    Given I am logged in as a valid user

Scenario: AC1 - Cold start (no query)
    Given there are existing tasks
    When I load the explore page
    Then the feed shows unseen trending content first, then seen trending content

Scenario: AC2 - Search returns results
    Given there is at least one item matching the query "Nebula"
    When I search for "Nebula"
    Then the results are ordered by match strength, then by interaction count

Scenario: AC3 - Pagination loads the next page with no duplicates
    Given there are more tasks than fit on a single page
    When I load page 1 of the explore feed
    And I load page 2 of the explore feed
    Then the second page contains no items from the first page

Scenario: AC5 - End of feed is reached when no more content is available
    Given there are existing tasks
    When I load pages of the explore feed until no more content is available
    Then the final page indicates no more content is available