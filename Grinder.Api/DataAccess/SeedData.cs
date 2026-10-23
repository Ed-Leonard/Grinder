using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.DataAccess;

public static class SeedData
{
    public static void Initialize(DatabaseContext db, IPasswordHasher<User> hasher)
    {
        if (!db.Reactions.Any())
        {
            db.Reactions.AddRange(
                new Reaction { Name = "4hfriday", FileName = "4hfriday.png" },
                new Reaction { Name = "67", FileName = "67.gif" },
                new Reaction { Name = "1984", FileName = "1984.gif" },
                new Reaction { Name = "absolute_cinema", FileName = "absolute_cinema.png" },
                new Reaction { Name = "ai", FileName = "ai.gif" },
                new Reaction { Name = "aura_farming", FileName = "aura_farming.gif" },
                new Reaction { Name = "breakdown", FileName = "breakdown.gif" },
                new Reaction { Name = "calculate", FileName = "calculate.gif" },
                new Reaction { Name = "cat_caught", FileName = "cat_caught.gif" },
                new Reaction { Name = "cat_dance_alt", FileName = "cat_dance_alt.gif" },
                new Reaction { Name = "cat_dance", FileName = "cat_dance.gif" },
                new Reaction { Name = "cat_jam_cry", FileName = "cat_jam_cry.gif" },
                new Reaction { Name = "cat_jam", FileName = "cat_jam.gif" },
                new Reaction { Name = "cat_kiss", FileName = "cat_kiss.gif" },
                new Reaction { Name = "cat_smile", FileName = "cat_smile.png" },
                new Reaction { Name = "confused_dog", FileName = "confused_dog.gif" },
                new Reaction { Name = "cooked", FileName = "cooked.png" },
                new Reaction { Name = "elmo_fire", FileName = "elmo_fire.gif" },
                new Reaction { Name = "extreme_teamwork", FileName = "extreme_teamwork.gif" },
                new Reaction { Name = "fabian_intensifies", FileName = "fabian_intensifies.gif" },
                new Reaction { Name = "fabian_salute", FileName = "fabian_salute.png" },
                new Reaction { Name = "frfr", FileName = "frfr.png" },
                new Reaction { Name = "frog_wow", FileName = "frog_wow.gif" },
                new Reaction { Name = "galaxy_brain", FileName = "galaxy_brain.gif" },
                new Reaction { Name = "glorp_explode", FileName = "glorp_explode.gif" },
                new Reaction { Name = "glorpdance2", FileName = "glorpdance2.gif" },
                new Reaction { Name = "heart_eye_morg", FileName = "heart_eye_morg.png" },
                new Reaction { Name = "heart_hands", FileName = "heart_hands.png" },
                new Reaction { Name = "loading", FileName = "loading.gif" },
                new Reaction { Name = "non", FileName = "non.png" },
                new Reaction { Name = "noo", FileName = "noo.gif" },
                new Reaction { Name = "oui", FileName = "oui.png" },
                new Reaction { Name = "party_parrot_cool", FileName = "party_parrot_cool.gif" },
                new Reaction { Name = "party_parrot", FileName = "party_parrot.gif" },
                new Reaction { Name = "polar_walk", FileName = "polar_walk.gif" },
                new Reaction { Name = "rabbit_vibe", FileName = "rabbit_vibe.gif" },
                new Reaction { Name = "sisyphus", FileName = "sisyphus.png" },
                new Reaction { Name = "skelebash", FileName = "skelebash.gif" },
                new Reaction { Name = "stick_dance", FileName = "stick_dance.gif" },
                new Reaction { Name = "timeout", FileName = "timeout.gif" },
                new Reaction { Name = "typing_cat", FileName = "typing_cat.gif" },
                new Reaction { Name = "waiting", FileName = "waiting.png" },
                new Reaction { Name = "yippee_cat", FileName = "yippee_cat.gif" },
                new Reaction { Name = "angel_fab", FileName = "angel_fab.png" },
                new Reaction { Name = "angel_morg", FileName = "angel_morg.png" }
            );
            db.SaveChanges();
        }


        if (!db.Badges.Any())
        {
            // Every badge here must be backed by data BadgeProgressEvaluator can actually check today.
            db.Badges.AddRange(
                new Badge
                {
                    Name = "The Grinder",
                    Description = "Awarded for moving 10 tasks into Done status",
                    Icon = "grinder",
                    CriteriaKey = BadgeCriteriaKeys.TasksDone,
                    Threshold = 10
                },
                new Badge
                {
                    Name = "Cleared the Board",
                    Description = "Awarded for having 100 tasks in Done status.",
                    Icon = "broom",
                    CriteriaKey = BadgeCriteriaKeys.TasksDone,
                    Threshold = 100
                },
                new Badge
                {
                    Name = "Flawless",
                    Description = "Awarded for creating 25 tasks",
                    Icon = "stars",
                    CriteriaKey = BadgeCriteriaKeys.TasksCreated,
                    Threshold = 25
                },
                new Badge
                {
                    Name = "The Procrastinator",
                    Description = "Awarded for having 3 tasks go overdue",
                    Icon = "snail",
                    CriteriaKey = BadgeCriteriaKeys.TasksOverdue,
                    Threshold = 3
                },
                new Badge
                {
                    Name = "Camera Ready",
                    Description = "Awarded for creating a clip",
                    Icon = "camera",
                    CriteriaKey = BadgeCriteriaKeys.ClipsCreated,
                    Threshold = 1
                },
                new Badge
                {
                    Name = "Divide and Conquer",
                    Description = "Awarded for creating 5 subtasks",
                    Icon = "divide",
                    CriteriaKey = BadgeCriteriaKeys.SubtasksCreated,
                    Threshold = 5
                },
                new Badge
                {
                    Name = "Well Organized",
                    Description = "Awarded for categorizing 25 tasks",
                    Icon = "notebook",
                    CriteriaKey = BadgeCriteriaKeys.TasksCategorized,
                    Threshold = 25
                },
                new Badge
                {
                    Name = "Making Friends",
                    Description = "Awarded for making 10 friends",
                    Icon = "friends",
                    CriteriaKey = BadgeCriteriaKeys.FriendsMade,
                    Threshold = 10
                }
            );

            db.SaveChanges();
        }
        if (db.Users.Any(u => u.Email == "john@example.com"))
        {
            SeedDirectMessages(db);
            SeedJaneWrappedData(db);
            return;
        }

        var john = new User { Email = "john@example.com", DisplayName = "John", Country = "New Zealand", IsVerified = true };
        john.PasswordHash = hasher.HashPassword(john, "P4$$word");
        var jane = new User { Email = "jane@example.com", DisplayName = "Jane", Country = "New Zealand", IsVerified = true };
        jane.PasswordHash = hasher.HashPassword(jane, "P4$$word");
        var alex = new User { Email = "alex@example.com", DisplayName = "Alex", Country = "New Zealand", IsVerified = true };
        alex.PasswordHash = hasher.HashPassword(alex, "P4$$word");
        var sam = new User { Email = "sam@example.com", DisplayName = "Sam", Country = "New Zealand", IsVerified = true };
        sam.PasswordHash = hasher.HashPassword(sam, "P4$$word");
        var priya = new User { Email = "priya@example.com", DisplayName = "Priya", Country = "New Zealand", IsVerified = true };
        priya.PasswordHash = hasher.HashPassword(priya, "P4$$word");
        var maria = new User { Email = "maria@example.com", DisplayName = "Maria", Country = "New Zealand", IsVerified = true };
        maria.PasswordHash = hasher.HashPassword(maria, "P4$$word");
        var liam = new User { Email = "liam@example.com", DisplayName = "Liam", Country = "New Zealand", IsVerified = true };
        liam.PasswordHash = hasher.HashPassword(liam, "P4$$word");
        var zoe = new User { Email = "zoe@example.com", DisplayName = "Zoe", Country = "New Zealand", IsVerified = true };
        zoe.PasswordHash = hasher.HashPassword(zoe, "P4$$word");
        var noah = new User { Email = "noah@example.com", DisplayName = "Noah", Country = "New Zealand", IsVerified = true };
        noah.PasswordHash = hasher.HashPassword(noah, "P4$$word");
        var chen = new User { Email = "chen@example.com", DisplayName = "Chen", Country = "New Zealand", IsVerified = true };
        chen.PasswordHash = hasher.HashPassword(chen, "P4$$word");


        // NEW users
        var olivia = new User { Email = "olivia@example.com", DisplayName = "Olivia", Country = "New Zealand", IsVerified = true };
        olivia.PasswordHash = hasher.HashPassword(olivia, "P4$$word");
        var ben = new User { Email = "ben@example.com", DisplayName = "Ben", Country = "New Zealand", IsVerified = true };
        ben.PasswordHash = hasher.HashPassword(ben, "P4$$word");
        var kate = new User { Email = "kate@example.com", DisplayName = "Kate", Country = "New Zealand", IsVerified = true };
        kate.PasswordHash = hasher.HashPassword(kate, "P4$$word");
        var raj = new User { Email = "raj@example.com", DisplayName = "Raj", Country = "New Zealand", IsVerified = true };
        raj.PasswordHash = hasher.HashPassword(raj, "P4$$word");
        var mia = new User { Email = "mia@example.com", DisplayName = "Mia", Country = "New Zealand", IsVerified = true };
        mia.PasswordHash = hasher.HashPassword(mia, "P4$$word");
        var tane = new User { Email = "tane@example.com", DisplayName = "Tane", Country = "New Zealand", IsVerified = true };
        tane.PasswordHash = hasher.HashPassword(tane, "P4$$word");
        var ella = new User { Email = "ella@example.com", DisplayName = "Ella", Country = "New Zealand", IsVerified = true };
        ella.PasswordHash = hasher.HashPassword(ella, "P4$$word");
        var jack = new User { Email = "jack@example.com", DisplayName = "Jack", Country = "New Zealand", IsVerified = true };
        jack.PasswordHash = hasher.HashPassword(jack, "P4$$word");
        var zane = new User { Email = "zane@example.com", DisplayName = "Zane", Country = "New Zealand", IsVerified = true };
        zane.PasswordHash = hasher.HashPassword(zane, "P4$$word");
        var dana = new User { Email = "dana@example.com", DisplayName = "Dana", Country = "New Zealand", IsVerified = true };
        dana.PasswordHash = hasher.HashPassword(dana, "P4$$word");
        var anna = new User { Email = "anna@example.com", DisplayName = "Anna", Country = "New Zealand", IsVerified = true };
        anna.PasswordHash = hasher.HashPassword(anna, "P4$$word");

        db.Users.AddRange(
            john, jane, alex, sam, priya, maria, liam, zoe, noah, chen,
            olivia, ben, kate, raj, mia, tane, ella, jack, zane, dana, anna
        );
        db.SaveChanges(); // commit users first so Id's are populated

        var work = new Category { Name = "Work" };
        var personal = new Category { Name = "Personal" };
        var shopping = new Category { Name = "Shopping" };
        var university = new Category { Name = "University" };
        var fitness = new Category { Name = "Fitness" };
        var home = new Category { Name = "Home" };

        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = DateTime.UtcNow;

        db.TaskItems.AddRange(
            // John
            new TaskItem { Title = "Buy groceries", Description = "Milk, eggs, bread, and whatever's on special for dinner this week.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [shopping] },
            new TaskItem { Title = "Do the laundry", Description = "Whites first, then the rest. Don't forget to actually hang it out this time.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [home] },
            new TaskItem { Title = "Cook wife a delicious meal", Description = "My wife is such a hard worker and is so lovely. I am going to make her her favourite meal ever as a surprise. Three courses, her favourite wine, the whole thing.", DueDate = DateOnly.MaxValue, DueTime = TimeOnly.MaxValue, ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = john.Id, Categories = [personal, home] },
            new TaskItem { Title = "Finally repainted the garage door", Description = "Been putting this off for a year, looks brand new now. Two coats of primer and a satin white finish, way happier with it than I expected to be.", DueTime = new TimeOnly(22, 59), DueDate = today.AddDays(-14), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = john.Id, Categories = [home] },
            new TaskItem { Title = "Adopt every single cat", Description = "A long-term goal, admittedly. Starting with one and seeing how it goes.", DueTime = new TimeOnly(22, 59), DueDate = today.AddDays(90), Visibility = TaskVisibility.Public, UserId = john.Id, Categories = [personal] },
            new TaskItem { Title = "Vote in the upcoming election", Description = "Enrolment's sorted, just need to actually get to the polling place this time.", DueTime = new TimeOnly(22, 59), DueDate = today.AddDays(10), Visibility = TaskVisibility.Public, UserId = john.Id, Categories = [personal] },

            // Jane
            new TaskItem { Title = "Get milk", Description = "We are, once again, completely out.", DueTime = new TimeOnly(22, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [shopping] },
            new TaskItem { Title = "Mow Lawns", Description = "Front and back before it rains again this weekend.", DueTime = new TimeOnly(13, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.FriendsOnly, UserId = jane.Id, Categories = [home] },
            new TaskItem { Title = "Remove weeds from the garden", Description = "Spent the whole afternoon on my knees pulling these out. The vege patch finally looks like a vege patch again instead of a jungle.", DueTime = new TimeOnly(22, 59), DueDate = today.AddDays(-5), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = jane.Id, Categories = [home] },
            new TaskItem { Title = "Trim the hedges", Description = "The neighbours have definitely been side-eyeing these for weeks now.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = jane.Id, Categories = [home] },
            new TaskItem { Title = "SENG365 Assignment 2", Description = "Refactor entire project to use components. Due Friday and I've barely started, classic.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [university] },
            new TaskItem { Title = "SENG301 Assignment 4", Description = "Group project write-up, still waiting on two teammates for their sections.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [university] },
            new TaskItem { Title = "COSC363 Assignment 2", Description = "Make a torus look epic. Currently it looks like a slightly deflated donut, so there's work to do.", DueTime = new TimeOnly(10, 0), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [university] },
            new TaskItem { Title = "Ran my first 10k this morning!", Description = "Legs are jelly but so worth it. Six months ago I couldn't run for the bus, now look at me. Onwards to a half marathon I guess.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = jane.Id, Categories = [fitness] },

            // Alex
            new TaskItem { Title = "New deadlift PR today", Description = "140kg, finally broke through the plateau I've been stuck at for two months. Coach reckons 150 is doable by summer if I keep this up.", DueTime = new TimeOnly(23, 59), DueDate = today, ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = alex.Id, Categories = [fitness] },
            new TaskItem { Title = "Meal prep for the week", Description = "Chicken, rice, and roast veges times five. Same as always but it works.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [fitness] },
            new TaskItem { Title = "Fix the leaky kitchen tap", Description = "Dripping non-stop for a week now, finally ordered the washer kit.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [home] },
            new TaskItem { Title = "Weekend hike up the Port Hills", Description = "Perfect clear day for it, views all the way out to the harbour. Definitely doing this loop again before winter sets in.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-2), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = alex.Id, Categories = [fitness] },
            new TaskItem { Title = "Read a chapter of Atomic Habits", Description = "On chapter six now, trying to actually apply this stuff instead of just highlighting it.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [personal] },

            // Sam
            new TaskItem { Title = "Adopted a kitten from the shelter!", Description = "Meet Waffles. He's been home for two hours and has already knocked three things off the bench, no regrets whatsoever.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-3), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = sam.Id, Categories = [personal] },
            new TaskItem { Title = "Clean the garage", Description = "It has become a black hole for cardboard boxes and I need it back.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(14), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [home] },
            new TaskItem { Title = "Sunday morning bike ride", Description = "Out the door before sunrise to beat the traffic. Coffee stop at the halfway point is non-negotiable at this point.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-2), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = sam.Id, Categories = [fitness] },
            new TaskItem { Title = "Renew car registration", Description = "Reminder text came through, sorting it online before the fine kicks in.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [personal] },
            new TaskItem { Title = "Call mum for her birthday", Description = "Don't forget this time, set three separate reminders just in case.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [personal] },

            // Priya
            new TaskItem { Title = "Practicing my latte art", Description = "Still can't get the rosetta right, but this swan attempt turned out better than I expected. Milk temperature is the whole game apparently.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = priya.Id, Categories = [personal] },
            new TaskItem { Title = "Finish tax return", Description = "Putting this off every year and every year I regret it come deadline week.", DueDate = today.AddDays(20), Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [personal] },
            new TaskItem { Title = "Beach day with the flatmates", Description = "Perfect way to spend a Saturday, way overdue for all of us to get out of the flat together. Already planning the next one.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-4), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = priya.Id, Categories = [personal] },
            new TaskItem { Title = "Organise the bookshelf", Description = "By colour this time, purely for the aesthetic, I know it's not practical.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(10), Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [home] },
            new TaskItem { Title = "Book flights for the trip", Description = "Prices keep going up the longer I wait so this needs to happen this week.", DueTime = new TimeOnly(21, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [personal] },

            // Maria
            new TaskItem { Title = "Baked my first sourdough loaf", Description = "Three days of feeding a starter finally paid off. Crust came out crackly and the crumb is actually open, genuinely proud of this one.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-6), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = maria.Id, Categories = [personal] },
            new TaskItem { Title = "Locked in for finals week", Description = "Third coffee of the day and it's only 2pm. Two exams down, three to go, send help.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = maria.Id, Categories = [university] },
            new TaskItem { Title = "Water the plants", Description = "The fern is looking sad again, it really does not let me forget.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [home] },
            new TaskItem { Title = "Pay power bill", Description = "Due Thursday, need to log in and actually do it instead of just seeing the email.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [home] },
            new TaskItem { Title = "Plan flatmate's surprise party", Description = "Guest list is sorted, now just need to find a way to get her out of the house for a few hours.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(12), Visibility = TaskVisibility.FriendsOnly, UserId = maria.Id, Categories = [personal] },

            // Liam
            new TaskItem { Title = "Chest day at the gym", Description = "Bench, incline dumbbell press, and way too many push-ups to finish it off.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = liam.Id, Categories = [fitness] },
            new TaskItem { Title = "Fix bike puncture", Description = "Second flat this month, might just be time for new tyres.", DueTime = new TimeOnly(22, 0), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [home] },
            new TaskItem { Title = "Long ride out to Lyttelton", Description = "Tackled the tunnel road climb for the first time, legs are done but it was worth it for the harbour view.", DueTime = new TimeOnly(16, 0), DueDate = today.AddDays(-7), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = liam.Id, Categories = [fitness] },
            new TaskItem { Title = "Clean out the garage", Description = "Same story as everyone else's garage apparently, it's a disaster in there.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(14), Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [home] },
            new TaskItem { Title = "Meal prep Sunday", Description = "Trying out a new curry recipe for this week's batch instead of the usual.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [fitness] },

            // Zoe
            new TaskItem { Title = "Finished the feature branch", Description = "Two weeks of work finally merged into main, tests all green.", DueTime = new TimeOnly(12, 59), DueDate = today.AddDays(-3), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [work] },
            new TaskItem { Title = "Sunset walk along the beach", Description = "Best way to decompress after a long week of debugging. Sky was unreal tonight.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = zoe.Id, Categories = [personal] },
            new TaskItem { Title = "Book the dentist", Description = "Overdue by about four months at this point, need to just call them.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-120), Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [personal] },
            new TaskItem { Title = "Learn 5 new Spanish words", Description = "Trying to actually stick with the app streak this time instead of giving up after a week.", DueTime = new TimeOnly(17, 59), DueDate = today.AddDays(30), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [personal] },

            // Noah
            new TaskItem { Title = "Repotted all the houseplants", Description = "Overdue by about a year for most of them, roots were completely bound up in there.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-30), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = noah.Id, Categories = [home] },
            new TaskItem { Title = "Oil change for the car", Description = "Overdue by a couple thousand km, booking it in before the next road trip.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-10), Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [home] },
            new TaskItem { Title = "Finish the bookshelf build", Description = "Frame's up, just need to sand it down and get a coat of varnish on before it goes in the lounge.", DueTime = new TimeOnly(8, 59), DueDate = today.AddDays(5), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = noah.Id, Categories = [home] },
            new TaskItem { Title = "Unsubscribe from junk mail", Description = "Inbox has become unusable, doing a proper cull this weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [personal] },

            // Chen (originally had no mutual friends -> now gets some, see follows below)
            new TaskItem { Title = "Set up new laptop", Description = "Migrating everything over from the old one, dreading the software reinstalls.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [work] },
            new TaskItem { Title = "First week at the new job", Description = "Survived it. Everyone's been really welcoming and I'm slowly figuring out where everything is.", DueTime = new TimeOnly(19, 59), DueDate = today.AddDays(-2), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = chen.Id, Categories = [work] },
            new TaskItem { Title = "Find a flat closer to work", Description = "The commute is killing me, viewing three places this weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [personal] },

            // Olivia
            new TaskItem { Title = "Finished my first half marathon", Description = "Legs are wrecked but honestly can't stop smiling. Next goal is under two hours.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-2), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = olivia.Id, Categories = [fitness] },
            new TaskItem { Title = "Repaint the spare room", Description = "Going with a soft sage green, swatches are up on the wall to decide.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.FriendsOnly, UserId = olivia.Id, Categories = [home] },
            new TaskItem { Title = "Submit expense report", Description = "Overdue by two weeks, finance has already sent one polite nudge email.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [work] },
            new TaskItem { Title = "Try that new ramen place", Description = "Everyone's been raving about it, finally got a booking for Friday night.", DueTime = new TimeOnly(20, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Public, UserId = olivia.Id, Categories = [personal] },
            new TaskItem { Title = "Weekly grocery run", Description = "Meal plan sorted, just need to actually stick to the list this time.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [shopping] },

            // Ben
            new TaskItem { Title = "Landed my first freelance client", Description = "Small logo and branding job but it's a start. Invoice sent, fingers crossed for a good review.", DueTime = new TimeOnly(20, 0), DueDate = today.AddDays(-3), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = ben.Id, Categories = [work] },
            new TaskItem { Title = "Fix squeaky door hinge", Description = "Been driving me up the wall for a month, finally bought the oil.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [home] },
            new TaskItem { Title = "Surf session at sunrise", Description = "Small clean waves and barely anyone else out. Perfect way to start a Saturday.", DueTime = new TimeOnly(12, 59), DueDate = today.AddDays(-5), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = ben.Id, Categories = [fitness] },
            new TaskItem { Title = "Update portfolio website", Description = "Long overdue, still showing projects from two jobs ago.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(9), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = ben.Id, Categories = [work] },

            // Kate
            new TaskItem { Title = "Started learning the guitar", Description = "Fingers are already sore after twenty minutes, apparently that's normal. Chords sound rough but recognisable.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Public, UserId = kate.Id, Categories = [personal] },
            new TaskItem { Title = "Deep clean the fridge", Description = "Found something in the back that I genuinely cannot identify anymore.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [home] },
            new TaskItem { Title = "Finish reading The Song of Achilles", Description = "About fifty pages from the end and trying not to cry on the bus.", DueTime = new TimeOnly(22, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = kate.Id, Categories = [personal] },
            new TaskItem { Title = "Book dentist checkup", Description = "Six month reminder finally came through, booking it before I forget again.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [personal] },

            // Raj
            new TaskItem { Title = "Shipped v2 of the side project", Description = "Been chipping away at this for three months in the evenings, finally live. Onto the next feature.", DueTime = new TimeOnly(18, 59), DueDate = today.AddDays(-4), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = raj.Id, Categories = [work, personal] },
            new TaskItem { Title = "Set up home network switch", Description = "Wi-Fi's been patchy in the office, running an ethernet line instead.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [home] },
            new TaskItem { Title = "Cricket nets on Thursday", Description = "First session back after the shoulder injury, taking it easy to start.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = raj.Id, Categories = [fitness] },
            new TaskItem { Title = "Renew passport", Description = "Expires in four months, cutting it fine but should be fine if I do it now.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(10), Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [personal] },

            // Mia
            new TaskItem { Title = "Adopted a rescue greyhound", Description = "Meet Biscuit. Retired racer, sleeps twenty hours a day, already the calmest housemate I've ever had.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-6), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = mia.Id, Categories = [personal] },
            new TaskItem { Title = "Assignment feedback review", Description = "Marking finally done for the semester, results going out Monday.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = mia.Id, Categories = [university] },
            new TaskItem { Title = "Weekend pottery class", Description = "Made a bowl that is, generously, bowl-shaped. Instructor was very encouraging about it.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-2), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = mia.Id, Categories = [personal] },

            // Tane
            new TaskItem { Title = "Placed in the club fun run", Description = "Third overall, not bad for barely training this month. Chasing top spot next time.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-3), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = tane.Id, Categories = [fitness] },
            new TaskItem { Title = "Fix the leaking gutter", Description = "Noticed it during last week's storm, ordering the sealant this weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = tane.Id, Categories = [home] },
            new TaskItem { Title = "Family BBQ this weekend", Description = "Whole extended whanau coming over, need to sort the shopping list.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = tane.Id, Categories = [personal] },

            // Ella
            new TaskItem { Title = "Opened an Etsy shop", Description = "Selling the ceramics I've been making in my garage for the past year. Terrified but excited to see if anyone buys anything.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = ella.Id, Categories = [work, personal] },
            new TaskItem { Title = "Restring the ukulele", Description = "Snapped a string mid-song at the worst possible moment during practice.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = ella.Id, Categories = [personal] },
            new TaskItem { Title = "Plan the flat's herb garden", Description = "Basil, mint, and coriander to start, might be too ambitious for a balcony this size.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(8), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = ella.Id, Categories = [home] },

            // Jack (deliberately sparse -> tests users with minimal content)
            new TaskItem { Title = "Set up budgeting spreadsheet", Description = "Finally tracking where the money actually goes instead of guessing.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = jack.Id, Categories = [personal] },
            new TaskItem { Title = "First open mic night", Description = "Terrifying and I forgot half the lyrics but I did it. Already thinking about the next one.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(-1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Public, UserId = jack.Id, Categories = [personal] },


    // John
    new TaskItem { Title = "Wash the car", Description = "Give the car a proper clean this weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [home] },
    new TaskItem { Title = "Book a haircut", Description = "Getting a bit overdue for one.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [personal] },
    new TaskItem { Title = "Finish the book", Description = "Only a few chapters left to go.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(8), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = john.Id, Categories = [personal] },
    new TaskItem { Title = "Clean the kitchen", Description = "Do the dishes and wipe down the benches.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [home] },

    // Jane
    new TaskItem { Title = "Buy birthday present", Description = "Find something nice before the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [shopping] },
    new TaskItem { Title = "Finish lecture notes", Description = "Catch up on this week's lectures.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [university] },
    new TaskItem { Title = "Go for a run", Description = "Easy five kilometre run after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = jane.Id, Categories = [fitness] },
    new TaskItem { Title = "Clean bedroom", Description = "Tidy everything up and change the sheets.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [home] },

    // Alex
    new TaskItem { Title = "Buy new gym shoes", Description = "Current ones are starting to fall apart.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [shopping, fitness] },
    new TaskItem { Title = "Stretch after training", Description = "Spend at least fifteen minutes stretching.", DueTime = new TimeOnly(23, 59), DueDate = today, ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [fitness] },
    new TaskItem { Title = "Clean the bathroom", Description = "Give everything a proper scrub.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [home] },
    new TaskItem { Title = "Plan next week's workouts", Description = "Work out the training schedule for the week.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [fitness] },

    // Sam
    new TaskItem { Title = "Buy cat food", Description = "Running low, pick some up after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [shopping] },
    new TaskItem { Title = "Clean the kitchen", Description = "The benches need a good clean.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [home] },
    new TaskItem { Title = "Watch a movie", Description = "Take the evening off and watch something good.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = sam.Id, Categories = [personal] },
    new TaskItem { Title = "Book a service for the car", Description = "Get the next service booked before the road trip.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [home] },

    // Priya
    new TaskItem { Title = "Buy groceries", Description = "Pick up everything needed for the week.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [shopping] },
    new TaskItem { Title = "Clean the balcony", Description = "Sweep everything and clean the outdoor furniture.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [home] },
    new TaskItem { Title = "Go to yoga", Description = "Evening yoga class after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = priya.Id, Categories = [fitness] },
    new TaskItem { Title = "Reply to emails", Description = "Clear out the inbox before the end of the day.", DueDate = today, ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [work] },

    // Maria
    new TaskItem { Title = "Study for exam", Description = "Review the notes from the last few weeks.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [university] },
    new TaskItem { Title = "Buy printer paper", Description = "Almost completely out at home.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [shopping] },
    new TaskItem { Title = "Call grandparents", Description = "Give them a call this weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = maria.Id, Categories = [personal] },
    new TaskItem { Title = "Clean the lounge", Description = "Tidy up before friends come over.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [home] },

    // Liam
    new TaskItem { Title = "Go for a bike ride", Description = "Take the bike out for an easy ride.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = liam.Id, Categories = [fitness] },
    new TaskItem { Title = "Order bike parts", Description = "Get the replacement parts for the bike.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [shopping] },
    new TaskItem { Title = "Clean the garage", Description = "Clear out the boxes taking up space.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(9), Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [home] },
    new TaskItem { Title = "Try a new recipe", Description = "Cook something different for dinner.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = liam.Id, Categories = [personal] },

    // Zoe
    new TaskItem { Title = "Fix the login bug", Description = "Track down the issue with user login.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [work] },
    new TaskItem { Title = "Update dependencies", Description = "Update the outdated packages in the project.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [work] },
    new TaskItem { Title = "Go for a walk", Description = "Get outside for an hour after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = zoe.Id, Categories = [fitness] },
    new TaskItem { Title = "Organise desk", Description = "Clear away old papers and cables.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [home] },

    // Noah
    new TaskItem { Title = "Buy light bulbs", Description = "Replace the two that have blown.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [shopping, home] },
    new TaskItem { Title = "Paint the hallway", Description = "Touch up the walls in the hallway.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(10), Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [home] },
    new TaskItem { Title = "Go fishing", Description = "Early morning fishing trip with friends.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), Visibility = TaskVisibility.FriendsOnly, UserId = noah.Id, Categories = [personal] },
    new TaskItem { Title = "Pay phone bill", Description = "Pay this month's phone bill.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [personal] },

    // Chen
    new TaskItem { Title = "Finish onboarding", Description = "Complete the remaining onboarding tasks.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [work] },
    new TaskItem { Title = "Meet the new team", Description = "Grab coffee with the team after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.FriendsOnly, UserId = chen.Id, Categories = [work] },
    new TaskItem { Title = "Buy desk chair", Description = "Find a better chair for the home office.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(8), Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [shopping, home] },
    new TaskItem { Title = "Clean the apartment", Description = "Do a full clean before the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [home] },

    // Olivia
    new TaskItem { Title = "Go for a recovery run", Description = "Easy run after the half marathon.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.FriendsOnly, UserId = olivia.Id, Categories = [fitness] },
    new TaskItem { Title = "Buy paint supplies", Description = "Get rollers, brushes and masking tape.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [shopping, home] },
    new TaskItem { Title = "Book accommodation", Description = "Sort somewhere to stay for the trip.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [personal] },
    new TaskItem { Title = "Clean out wardrobe", Description = "Donate clothes that haven't been worn in years.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(12), Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [home] },

    // Ben
    new TaskItem { Title = "Finish client logo", Description = "Make the final changes before sending it over.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [work] },
    new TaskItem { Title = "Send invoice", Description = "Send the invoice for the completed work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [work] },
    new TaskItem { Title = "Go surfing", Description = "Check the forecast and head out early.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = ben.Id, Categories = [fitness] },
    new TaskItem { Title = "Buy groceries", Description = "Pick up food for the week.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [shopping] },

    // Kate
    new TaskItem { Title = "Practice guitar", Description = "Spend thirty minutes practising chords.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.FriendsOnly, UserId = kate.Id, Categories = [personal] },
    new TaskItem { Title = "Clean the bathroom", Description = "Give the bathroom a proper clean.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [home] },
    new TaskItem { Title = "Buy guitar strings", Description = "Pick up a spare set of strings.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [shopping] },
    new TaskItem { Title = "Finish assignment", Description = "Complete the final section and submit it.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [university] },

    // Raj
    new TaskItem { Title = "Fix the Wi-Fi", Description = "Move the router and test the connection.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [home] },
    new TaskItem { Title = "Push code changes", Description = "Commit and push the latest changes.", DueTime = new TimeOnly(23, 59), DueDate = today, Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [work] },
    new TaskItem { Title = "Go to cricket training", Description = "Evening training session at the club.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = raj.Id, Categories = [fitness] },
    new TaskItem { Title = "Buy new headphones", Description = "Current pair is starting to break.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [shopping] },

    // Mia
    new TaskItem { Title = "Take dog for a walk", Description = "Long walk around the park.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.FriendsOnly, UserId = mia.Id, Categories = [fitness] },
    new TaskItem { Title = "Prepare lecture", Description = "Finish preparing tomorrow's lecture.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = mia.Id, Categories = [university] },
    new TaskItem { Title = "Buy dog food", Description = "Pick up another bag before the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = mia.Id, Categories = [shopping] },
    new TaskItem { Title = "Clean the kitchen", Description = "Tidy everything after dinner.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = mia.Id, Categories = [home] },

    // Tane
    new TaskItem { Title = "Morning run", Description = "Easy five kilometre run before work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.FriendsOnly, UserId = tane.Id, Categories = [fitness] },
    new TaskItem { Title = "Fix the fence", Description = "Replace the loose section before it gets worse.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = tane.Id, Categories = [home] },
    new TaskItem { Title = "Buy BBQ supplies", Description = "Get meat, drinks and everything else needed.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = tane.Id, Categories = [shopping] },
    new TaskItem { Title = "Clean the car", Description = "Vacuum the inside and wash the outside.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.FriendsOnly, UserId = tane.Id, Categories = [home] },

    // Ella
    new TaskItem { Title = "Make new ceramics", Description = "Spend the afternoon working on some new pieces.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = ella.Id, Categories = [work] },
    new TaskItem { Title = "Package orders", Description = "Pack the latest orders ready for shipping.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = ella.Id, Categories = [work] },
    new TaskItem { Title = "Buy clay", Description = "Running low on clay for the next batch.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = ella.Id, Categories = [shopping] },
    new TaskItem { Title = "Water the herbs", Description = "The balcony plants need some water.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = ella.Id, Categories = [home] },

    // Jack
    new TaskItem { Title = "Practice for open mic", Description = "Run through the set a few times.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = jack.Id, Categories = [personal] },
    new TaskItem { Title = "Pay rent", Description = "Transfer rent for the month.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = jack.Id, Categories = [personal] },
    new TaskItem { Title = "Buy groceries", Description = "Get food for the rest of the week.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = jack.Id, Categories = [shopping] },
    new TaskItem { Title = "Clean the flat", Description = "Do a quick tidy before the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = jack.Id, Categories = [home] },

// More extra tasks

// John
new TaskItem { Title = "Take out the rubbish", Description = "Put the bins out before collection.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [home] },
new TaskItem { Title = "Buy coffee", Description = "Pick up some coffee beans.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [shopping] },
new TaskItem { Title = "Go for a walk", Description = "Get outside for a bit after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.FriendsOnly, UserId = john.Id, Categories = [fitness] },
new TaskItem { Title = "Clean the garage", Description = "Clear out some of the old boxes.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [home] },
new TaskItem { Title = "Finish work report", Description = "Complete the final section of the report.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = john.Id, Categories = [work] },

// Jane
new TaskItem { Title = "Buy new shoes", Description = "Find a comfortable pair for everyday use.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [shopping] },
new TaskItem { Title = "Read lecture slides", Description = "Go through the slides before the tutorial.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [university] },
new TaskItem { Title = "Clean the kitchen", Description = "Wash the dishes and wipe down the benches.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [home] },
new TaskItem { Title = "Evening gym session", Description = "Get a quick workout in after class.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = jane.Id, Categories = [fitness] },
new TaskItem { Title = "Submit tutorial work", Description = "Upload the completed tutorial before midnight.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = jane.Id, Categories = [university] },

// Alex
new TaskItem { Title = "Buy protein powder", Description = "Running low on the current tub.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [shopping, fitness] },
new TaskItem { Title = "Clean the fridge", Description = "Throw out anything that has gone bad.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [home] },
new TaskItem { Title = "Go swimming", Description = "Easy swim for some recovery work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.FriendsOnly, UserId = alex.Id, Categories = [fitness] },
new TaskItem { Title = "Prepare lunch", Description = "Make lunch for tomorrow.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [personal] },
new TaskItem { Title = "Order replacement parts", Description = "Order the parts needed for the repair.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = alex.Id, Categories = [shopping] },

// Sam
new TaskItem { Title = "Vacuum the house", Description = "Vacuum all the main rooms.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [home] },
new TaskItem { Title = "Buy birthday card", Description = "Find a card for the birthday this weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [shopping] },
new TaskItem { Title = "Play basketball", Description = "Meet friends for a game after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = sam.Id, Categories = [fitness] },
new TaskItem { Title = "Sort old photos", Description = "Go through the photos on the old laptop.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(8), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [personal] },
new TaskItem { Title = "Clean the windows", Description = "Give the windows a quick clean.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(10), Visibility = TaskVisibility.Private, UserId = sam.Id, Categories = [home] },

// Priya
new TaskItem { Title = "Buy ingredients", Description = "Pick up ingredients for dinner.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [shopping] },
new TaskItem { Title = "Finish presentation", Description = "Add the final slides and practise once.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [work] },
new TaskItem { Title = "Go for a swim", Description = "Do a few easy laps at the pool.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = priya.Id, Categories = [fitness] },
new TaskItem { Title = "Change bed sheets", Description = "Put fresh sheets on the bed.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = priya.Id, Categories = [home] },
new TaskItem { Title = "Catch up with a friend", Description = "Meet up for coffee after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.FriendsOnly, UserId = priya.Id, Categories = [personal] },

// Maria
new TaskItem { Title = "Finish reading notes", Description = "Review the remaining notes before the exam.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [university] },
new TaskItem { Title = "Buy snacks", Description = "Pick up snacks for study sessions.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [shopping] },
new TaskItem { Title = "Go for a run", Description = "Short run around the neighbourhood.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.FriendsOnly, UserId = maria.Id, Categories = [fitness] },
new TaskItem { Title = "Wash the car", Description = "Give the car a quick wash.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [home] },
new TaskItem { Title = "Submit assignment", Description = "Upload the final assignment before the deadline.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = maria.Id, Categories = [university] },

// Liam
new TaskItem { Title = "Clean bike", Description = "Wash the bike after the last ride.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [home] },
new TaskItem { Title = "Buy cycling gloves", Description = "Get a new pair before the next ride.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [shopping, fitness] },
new TaskItem { Title = "Go for a long ride", Description = "Plan a longer ride for the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.FriendsOnly, UserId = liam.Id, Categories = [fitness] },
new TaskItem { Title = "Fix the chain", Description = "Clean and adjust the bike chain.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = liam.Id, Categories = [home] },
new TaskItem { Title = "Try a new cafe", Description = "Check out the new cafe nearby.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Public, UserId = liam.Id, Categories = [personal] },

// Zoe
new TaskItem { Title = "Review pull request", Description = "Check the latest changes before merging.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [work] },
new TaskItem { Title = "Write unit tests", Description = "Add tests for the new feature.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [work] },
new TaskItem { Title = "Clean the office", Description = "Tidy the desk and organise the cables.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [home] },
new TaskItem { Title = "Go hiking", Description = "Head out for a short weekend hike.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), Visibility = TaskVisibility.FriendsOnly, UserId = zoe.Id, Categories = [fitness] },
new TaskItem { Title = "Learn new vocabulary", Description = "Spend twenty minutes learning new words.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = zoe.Id, Categories = [personal] },

// Noah
new TaskItem { Title = "Clean the shed", Description = "Sort out the tools and old boxes.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(8), Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [home] },
new TaskItem { Title = "Buy car wash supplies", Description = "Get soap and cleaning cloths.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [shopping] },
new TaskItem { Title = "Go camping", Description = "Head away for the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(10), Visibility = TaskVisibility.FriendsOnly, UserId = noah.Id, Categories = [personal] },
new TaskItem { Title = "Check tyre pressure", Description = "Check all four tyres before the trip.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [home] },
new TaskItem { Title = "Organise tools", Description = "Put the tools back where they belong.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = noah.Id, Categories = [home] },

// Chen
new TaskItem { Title = "Finish first project", Description = "Complete the remaining work on the project.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [work] },
new TaskItem { Title = "Set up desk", Description = "Get the home office ready for work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [home] },
new TaskItem { Title = "Buy office supplies", Description = "Pick up notebooks and pens.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [shopping] },
new TaskItem { Title = "Meet a friend for lunch", Description = "Catch up over lunch this weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.FriendsOnly, UserId = chen.Id, Categories = [personal] },
new TaskItem { Title = "Update CV", Description = "Add the new job and recent experience.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(9), Visibility = TaskVisibility.Private, UserId = chen.Id, Categories = [work] },

// Olivia
new TaskItem { Title = "Stretch after running", Description = "Spend some time recovering after training.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [fitness] },
new TaskItem { Title = "Buy groceries", Description = "Get everything needed for the week.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [shopping] },
new TaskItem { Title = "Paint the walls", Description = "Start painting the spare room.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = olivia.Id, Categories = [home] },
new TaskItem { Title = "Book hotel", Description = "Find and book somewhere to stay.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [personal] },
new TaskItem { Title = "Clean the spare room", Description = "Move everything out before painting.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = olivia.Id, Categories = [home] },

// Ben
new TaskItem { Title = "Finish website mockup", Description = "Complete the last few screens.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [work] },
new TaskItem { Title = "Reply to client", Description = "Send an update about the project.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [work] },
new TaskItem { Title = "Go for a swim", Description = "Get some exercise after work.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = ben.Id, Categories = [fitness] },
new TaskItem { Title = "Clean the kitchen", Description = "Do a quick clean before the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [home] },
new TaskItem { Title = "Buy a new backpack", Description = "Current one is starting to wear out.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(8), Visibility = TaskVisibility.Private, UserId = ben.Id, Categories = [shopping] },

// Kate
new TaskItem { Title = "Learn a new chord", Description = "Practise one new guitar chord.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.FriendsOnly, UserId = kate.Id, Categories = [personal] },
new TaskItem { Title = "Do the dishes", Description = "Clean up the dishes from dinner.", DueTime = new TimeOnly(23, 59), DueDate = today, ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [home] },
new TaskItem { Title = "Buy groceries", Description = "Pick up food for the week.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [shopping] },
new TaskItem { Title = "Read another chapter", Description = "Read one more chapter before bed.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.FriendsOnly, UserId = kate.Id, Categories = [personal] },
new TaskItem { Title = "Finish tutorial", Description = "Complete the remaining tutorial questions.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = kate.Id, Categories = [university] },

// Raj
new TaskItem { Title = "Restart the router", Description = "Try restarting everything before changing settings.", DueTime = new TimeOnly(23, 59), DueDate = today, ItemStatus = TaskItemStatus.Done, Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [home] },
new TaskItem { Title = "Finish documentation", Description = "Write documentation for the new feature.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [work] },
new TaskItem { Title = "Buy cricket gear", Description = "Replace some old training gear.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(6), Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [shopping, fitness] },
new TaskItem { Title = "Train at the nets", Description = "Get another practice session in.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = raj.Id, Categories = [fitness] },
new TaskItem { Title = "Clean the office", Description = "Tidy up the home office.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = raj.Id, Categories = [home] },

// Mia
new TaskItem { Title = "Walk the dog", Description = "Take Biscuit around the park.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.FriendsOnly, UserId = mia.Id, Categories = [fitness] },
new TaskItem { Title = "Prepare lesson", Description = "Finish the slides for tomorrow.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.Private, UserId = mia.Id, Categories = [university] },
new TaskItem { Title = "Buy dog treats", Description = "Pick up some treats for Biscuit.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = mia.Id, Categories = [shopping] },
new TaskItem { Title = "Clean the lounge", Description = "Tidy everything before the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = mia.Id, Categories = [home] },
new TaskItem { Title = "Read for an hour", Description = "Spend some quiet time reading.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.FriendsOnly, UserId = mia.Id, Categories = [personal] },

// Tane
new TaskItem { Title = "Go for a run", Description = "Easy run around the neighbourhood.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.FriendsOnly, UserId = tane.Id, Categories = [fitness] },
new TaskItem { Title = "Mow the lawn", Description = "Cut the grass before the weekend.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = tane.Id, Categories = [home] },
new TaskItem { Title = "Buy BBQ food", Description = "Get everything needed for the BBQ.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = tane.Id, Categories = [shopping] },
new TaskItem { Title = "Clean the deck", Description = "Sweep and wash the deck.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Private, UserId = tane.Id, Categories = [home] },
new TaskItem { Title = "Plan next race", Description = "Find a race to enter next month.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(8), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = tane.Id, Categories = [fitness] },

// Ella
new TaskItem { Title = "Make some mugs", Description = "Try making a few new mug designs.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = ella.Id, Categories = [work] },
new TaskItem { Title = "Pack customer orders", Description = "Pack and label the latest orders.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), Visibility = TaskVisibility.Private, UserId = ella.Id, Categories = [work] },
new TaskItem { Title = "Buy more clay", Description = "Get another bag for the next batch.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = ella.Id, Categories = [shopping] },
new TaskItem { Title = "Clean the studio", Description = "Tidy up the workspace after making ceramics.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = ella.Id, Categories = [home] },
new TaskItem { Title = "Take photos of products", Description = "Photograph the new pieces for the shop.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(5), Visibility = TaskVisibility.Public, UserId = ella.Id, Categories = [work] },

// Jack
new TaskItem { Title = "Practise guitar", Description = "Run through the songs for the next open mic.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(1), ItemStatus = TaskItemStatus.InProgress, Visibility = TaskVisibility.FriendsOnly, UserId = jack.Id, Categories = [personal] },
new TaskItem { Title = "Pay electricity bill", Description = "Pay the latest bill online.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(2), Visibility = TaskVisibility.Private, UserId = jack.Id, Categories = [home] },
new TaskItem { Title = "Buy groceries", Description = "Pick up food for the next few days.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(3), Visibility = TaskVisibility.Private, UserId = jack.Id, Categories = [shopping] },
new TaskItem { Title = "Clean the bathroom", Description = "Give the bathroom a quick clean.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(4), Visibility = TaskVisibility.Private, UserId = jack.Id, Categories = [home] },
new TaskItem { Title = "Learn a new song", Description = "Learn another song for the next open mic.", DueTime = new TimeOnly(23, 59), DueDate = today.AddDays(7), Visibility = TaskVisibility.Public, UserId = jack.Id, Categories = [personal] }

            );
        db.SaveChanges();

        db.Follow.AddRange(
            // Original mutual follows -> "friends"
            new Follow { FollowerId = john.Id, FollowedId = jane.Id },
            new Follow { FollowerId = jane.Id, FollowedId = john.Id },
            new Follow { FollowerId = john.Id, FollowedId = alex.Id },
            new Follow { FollowerId = alex.Id, FollowedId = john.Id },
            new Follow { FollowerId = john.Id, FollowedId = sam.Id },
            new Follow { FollowerId = sam.Id, FollowedId = john.Id },
            new Follow { FollowerId = john.Id, FollowedId = maria.Id },
            new Follow { FollowerId = maria.Id, FollowedId = john.Id },
            new Follow { FollowerId = jane.Id, FollowedId = alex.Id },
            new Follow { FollowerId = alex.Id, FollowedId = jane.Id },
            new Follow { FollowerId = jane.Id, FollowedId = maria.Id },
            new Follow { FollowerId = maria.Id, FollowedId = jane.Id },
            new Follow { FollowerId = jane.Id, FollowedId = sam.Id },
            new Follow { FollowerId = sam.Id, FollowedId = jane.Id },
            new Follow { FollowerId = jane.Id, FollowedId = zoe.Id },
            new Follow { FollowerId = zoe.Id, FollowedId = jane.Id },
            new Follow { FollowerId = alex.Id, FollowedId = maria.Id },
            new Follow { FollowerId = maria.Id, FollowedId = alex.Id },
            new Follow { FollowerId = alex.Id, FollowedId = sam.Id },
            new Follow { FollowerId = sam.Id, FollowedId = alex.Id },
            new Follow { FollowerId = sam.Id, FollowedId = liam.Id },
            new Follow { FollowerId = liam.Id, FollowedId = sam.Id },
            new Follow { FollowerId = liam.Id, FollowedId = noah.Id },
            new Follow { FollowerId = noah.Id, FollowedId = liam.Id },
            new Follow { FollowerId = zoe.Id, FollowedId = noah.Id },
            new Follow { FollowerId = noah.Id, FollowedId = zoe.Id },
            new Follow { FollowerId = priya.Id, FollowedId = maria.Id },
            new Follow { FollowerId = maria.Id, FollowedId = priya.Id },
            new Follow { FollowerId = priya.Id, FollowedId = zoe.Id },
            new Follow { FollowerId = zoe.Id, FollowedId = priya.Id },

            // One-directional follows -> NOT friends
            new Follow { FollowerId = sam.Id, FollowedId = priya.Id },
            new Follow { FollowerId = priya.Id, FollowedId = john.Id },
            new Follow { FollowerId = noah.Id, FollowedId = john.Id },

            // NEW mutual follows
            new Follow { FollowerId = john.Id, FollowedId = olivia.Id },
            new Follow { FollowerId = olivia.Id, FollowedId = john.Id },
            new Follow { FollowerId = john.Id, FollowedId = ben.Id },
            new Follow { FollowerId = ben.Id, FollowedId = john.Id },
            new Follow { FollowerId = jane.Id, FollowedId = kate.Id },
            new Follow { FollowerId = kate.Id, FollowedId = jane.Id },
            new Follow { FollowerId = alex.Id, FollowedId = raj.Id },
            new Follow { FollowerId = raj.Id, FollowedId = alex.Id },
            new Follow { FollowerId = alex.Id, FollowedId = tane.Id },
            new Follow { FollowerId = tane.Id, FollowedId = alex.Id },
            new Follow { FollowerId = maria.Id, FollowedId = mia.Id },
            new Follow { FollowerId = mia.Id, FollowedId = maria.Id },
            new Follow { FollowerId = liam.Id, FollowedId = tane.Id },
            new Follow { FollowerId = tane.Id, FollowedId = liam.Id },
            new Follow { FollowerId = liam.Id, FollowedId = raj.Id },
            new Follow { FollowerId = raj.Id, FollowedId = liam.Id },
            new Follow { FollowerId = zoe.Id, FollowedId = ella.Id },
            new Follow { FollowerId = ella.Id, FollowedId = zoe.Id },
            new Follow { FollowerId = noah.Id, FollowedId = ella.Id },
            new Follow { FollowerId = ella.Id, FollowedId = noah.Id },
            new Follow { FollowerId = priya.Id, FollowedId = mia.Id },
            new Follow { FollowerId = mia.Id, FollowedId = priya.Id },
            new Follow { FollowerId = chen.Id, FollowedId = olivia.Id },
            new Follow { FollowerId = olivia.Id, FollowedId = chen.Id },
            new Follow { FollowerId = chen.Id, FollowedId = ben.Id },
            new Follow { FollowerId = ben.Id, FollowedId = chen.Id },
            new Follow { FollowerId = kate.Id, FollowedId = mia.Id },
            new Follow { FollowerId = mia.Id, FollowedId = kate.Id },
            new Follow { FollowerId = raj.Id, FollowedId = jack.Id },
            new Follow { FollowerId = jack.Id, FollowedId = raj.Id },

            // NEW one-directional follows -> NOT friends, more asymmetric coverage
            new Follow { FollowerId = olivia.Id, FollowedId = jane.Id },
            new Follow { FollowerId = ben.Id, FollowedId = alex.Id },
            new Follow { FollowerId = kate.Id, FollowedId = olivia.Id },
            new Follow { FollowerId = ella.Id, FollowedId = tane.Id },
            new Follow { FollowerId = jack.Id, FollowedId = john.Id },
            new Follow { FollowerId = jack.Id, FollowedId = olivia.Id },
            new Follow { FollowerId = mia.Id, FollowedId = ben.Id }

            // jack has very few follows/followers -> tests a sparsely-connected user
            // tane has a small friend group -> tests a moderately-connected user
        );
        db.SaveChanges();


        // --- TaskViews: pre-seed some viewing history for John (userId used most often in manual/API testing) ---
        // These simulate John having already scrolled past a batch of public posts from non-friends,
        // so you can verify the "already seen" filter without hitting the /feed/viewed endpoint manually first.
        var oliviaHalfMarathon = db.TaskItems.First(t => t.Title == "Finished my first half marathon");
        var kateGuitar = db.TaskItems.First(t => t.Title == "Started learning the guitar");
        var raj2 = db.TaskItems.First(t => t.Title == "Shipped v2 of the side project");
        var miaGreyhound = db.TaskItems.First(t => t.Title == "Adopted a rescue greyhound");
        var janeFirst10k = db.TaskItems.First(t => t.Title == "Ran my first 10k this morning!");

        db.TaskViews.AddRange(
            new TaskView { UserId = john.Id, TaskItemId = oliviaHalfMarathon.Id, ViewedAt = now.AddDays(-2) },
            new TaskView { UserId = john.Id, TaskItemId = kateGuitar.Id, ViewedAt = now.AddDays(-1) },
            new TaskView { UserId = john.Id, TaskItemId = raj2.Id, ViewedAt = now.AddHours(-6) },
            // Mia's post viewed, then "edited" after the view -> should reappear as updated in John's feed
            new TaskView { UserId = john.Id, TaskItemId = miaGreyhound.Id, ViewedAt = now.AddDays(-3) },
            // Jane's public post viewed by John already (friend-pool item, still gets an IsUpdated check but no re-filtering)
            new TaskView { UserId = john.Id, TaskItemId = janeFirst10k.Id, ViewedAt = now.AddHours(-2) }
        );
        db.SaveChanges();

        // Bump Mia's greyhound post's UpdatedAt to simulate an edit that happened AFTER John already viewed it.
        // This should cause it to reappear in John's public feed pool, flagged as IsUpdated.
        miaGreyhound.UpdatedAt = now.AddHours(-1);
        db.SaveChanges();

        SeedDirectMessages(db);
        SeedJaneWrappedData(db);
    }

    /// <summary>
    /// Gives Jane a full Wrapped year: account age, completed dates, reactions,
    /// a most-replied comment, and at least five categories. Safe to re-run.
    /// Written by claude sonnet 5
    /// </summary>
    private static void SeedJaneWrappedData(DatabaseContext db)
    {
        var jane = db.Users.FirstOrDefault(u => u.Email == "jane@example.com");
        if (jane is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var yearStart = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        jane.CreatedAt = now.UtcDateTime.AddMonths(-8);

        var janeTasks = db.TaskItems
            .Include(t => t.Categories)
            .Where(t => t.UserId == jane.Id)
            .OrderBy(t => t.Id)
            .ToList();

        var dayOffset = 20;
        foreach (var task in janeTasks)
        {
            task.CreatedAt = yearStart.AddDays(dayOffset);
            dayOffset += 10;
            if (task.CreatedAt > now.AddDays(-2))
            {
                task.CreatedAt = now.AddDays(-2);
            }

            if (task.ItemStatus == TaskItemStatus.Done)
            {
                var completed = task.CreatedAt.AddDays(4);
                task.CompletedAt = completed > now ? now.AddDays(-1) : completed;
            }
        }

        var tenK = janeTasks.FirstOrDefault(t => t.Title == "Ran my first 10k this morning!");
        if (tenK is not null)
        {
            tenK.CreatedAt = now.AddMonths(-6);
            tenK.CompletedAt = now.AddDays(-1);
            tenK.ItemStatus = TaskItemStatus.Done;
        }

        var work = db.Category.FirstOrDefault(c => c.Name == "Work");
        var personal = db.Category.FirstOrDefault(c => c.Name == "Personal");
        if (work is not null && janeTasks.All(t => t.Title != "Shift at the cafe"))
        {
            db.TaskItems.Add(new TaskItem
            {
                Title = "Shift at the cafe",
                Description = "Covered the Saturday morning rush. Tips were decent.",
                DueDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-12)),
                DueTime = new TimeOnly(14, 0),
                ItemStatus = TaskItemStatus.Done,
                Visibility = TaskVisibility.FriendsOnly,
                UserId = jane.Id,
                Categories = [work],
                CreatedAt = now.AddMonths(-3),
                CompletedAt = now.AddDays(-12)
            });
        }

        // Give Jane a task completed sometime last calendar year, so she's
        // eligible for the previous year's Wrapped
        if (janeTasks.All(t => t.Title != "Grinder Wrapped 2025"))
        {
            var previousYear = now.Year - 1;
            db.TaskItems.Add(new TaskItem
            {
                Title = "Grinder Wrapped 2025",
                Description = "Check out my stats from last year, I was a busy bee.",
                DueDate = new DateOnly(previousYear, 6, 15),
                DueTime = new TimeOnly(23, 59),
                ItemStatus = TaskItemStatus.Done,
                Visibility = TaskVisibility.Public,
                UserId = jane.Id,
                Categories = personal is not null ? [personal] : [],
                CreatedAt = new DateTimeOffset(previousYear, 6, 1, 0, 0, 0, TimeSpan.Zero),
                CompletedAt = new DateTimeOffset(previousYear, 6, 15, 0, 0, 0, TimeSpan.Zero)
            });
        }

        if (personal is not null && janeTasks.All(t => t.Title != "Call mum"))
        {
            db.TaskItems.Add(new TaskItem
            {
                Title = "Call mum",
                Description = "Sunday catch-up, don't forget the time difference.",
                DueDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(2)),
                DueTime = new TimeOnly(19, 0),
                Visibility = TaskVisibility.Private,
                UserId = jane.Id,
                Categories = [personal],
                CreatedAt = now.AddMonths(-1)
            });
        }

        db.SaveChanges();

        tenK = db.TaskItems.FirstOrDefault(t => t.UserId == jane.Id && t.Title == "Ran my first 10k this morning!");
        var weeds = db.TaskItems.FirstOrDefault(t => t.UserId == jane.Id && t.Title == "Remove weeds from the garden");
        var reactions = db.Reactions.Take(4).ToList();
        var reactors = db.Users.Where(u => u.Id != jane.Id).Take(6).ToList();

        if (tenK is not null && reactions.Count > 0 && !db.TaskReactions.Any(r => r.TaskItemId == tenK.Id))
        {
            for (var i = 0; i < reactors.Count; i++)
            {
                db.TaskReactions.Add(new TaskReaction
                {
                    TaskItemId = tenK.Id,
                    ReactionId = reactions[i % reactions.Count].Id,
                    UserId = reactors[i].Id,
                    CreatedAt = now.AddDays(-i)
                });
            }
        }

        if (weeds is not null && reactions.Count > 0 && reactors.Count >= 2 && !db.TaskReactions.Any(r => r.TaskItemId == weeds.Id))
        {
            db.TaskReactions.Add(new TaskReaction
            {
                TaskItemId = weeds.Id,
                ReactionId = reactions[0].Id,
                UserId = reactors[0].Id,
                CreatedAt = now.AddDays(-3)
            });
            db.TaskReactions.Add(new TaskReaction
            {
                TaskItemId = weeds.Id,
                ReactionId = reactions[1].Id,
                UserId = reactors[1].Id,
                CreatedAt = now.AddDays(-2)
            });
        }

        if (tenK is not null && !db.Comments.Any(c => c.AuthorId == jane.Id && c.TaskId == tenK.Id))
        {
            var popular = new Comment
            {
                TaskId = tenK.Id,
                AuthorId = jane.Id,
                Content = "Can't believe I actually finished. Six months ago I couldn't run for the bus.",
                CreatedAt = now.AddDays(-1)
            };
            db.Comments.Add(popular);
            db.SaveChanges();

            foreach (var reactor in reactors.Take(4))
            {
                db.Comments.Add(new Comment
                {
                    TaskId = tenK.Id,
                    AuthorId = reactor.Id,
                    ParentCommentId = popular.Id,
                    Content = reactor.DisplayName == "John"
                        ? "Insane work Jane, so proud of you."
                        : "Legend. Next stop half marathon.",
                    CreatedAt = now.AddHours(-8)
                });
            }
        }

        if (weeds is not null && reactors.Count > 0 && !db.Comments.Any(c => c.AuthorId == jane.Id && c.TaskId == weeds.Id))
        {
            var quiet = new Comment
            {
                TaskId = weeds.Id,
                AuthorId = jane.Id,
                Content = "The vege patch finally looks like a vege patch again.",
                CreatedAt = now.AddDays(-4)
            };
            db.Comments.Add(quiet);
            db.SaveChanges();
            db.Comments.Add(new Comment
            {
                TaskId = weeds.Id,
                AuthorId = reactors[0].Id,
                ParentCommentId = quiet.Id,
                Content = "Looks so much better!",
                CreatedAt = now.AddDays(-3)
            });
        }

        db.SaveChanges();
    }

    // Made by claude
    private static void SeedDirectMessages(DatabaseContext db)
    {
        if (db.DirectMessages.Any())
        {
            return;
        }

        var john = db.Users.FirstOrDefault(u => u.Email == "john@example.com");
        var jane = db.Users.FirstOrDefault(u => u.Email == "jane@example.com");
        var alex = db.Users.FirstOrDefault(u => u.Email == "alex@example.com");
        var sam = db.Users.FirstOrDefault(u => u.Email == "sam@example.com");
        var maria = db.Users.FirstOrDefault(u => u.Email == "maria@example.com");
        var zoe = db.Users.FirstOrDefault(u => u.Email == "zoe@example.com");
        var kate = db.Users.FirstOrDefault(u => u.Email == "kate@example.com");
        if (john is null || jane is null || alex is null || sam is null || maria is null || zoe is null || kate is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.DirectMessages.AddRange(
            // John <-> Jane: two unread for Jane, one unread for John
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-5),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-4),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-40),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-5),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddMinutes(-4),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddHours(-40),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-98),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-50),
                IsRead = false
            },
                        new DirectMessage
                        {
                            SenderId = jane.Id,
                            ReceiverId = john.Id,
                            Content = "Hey, did you get milk yet?",
                            SentAt = now.AddHours(-2),
                            IsRead = true
                        },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-1),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-2000),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-10),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-2),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-2),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-30),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },

            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-90),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-1),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-50),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-5),
                IsRead = false
            },

            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-5),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-4),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-40),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-5),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-4),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-40),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-5),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddMinutes(-4),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddHours(-40),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-98),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-50),
                IsRead = false
            },
                        new DirectMessage
                        {
                            SenderId = jane.Id,
                            ReceiverId = john.Id,
                            Content = "Hey, did you get milk yet?",
                            SentAt = now.AddHours(-2),
                            IsRead = true
                        },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-1),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-2000),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-10),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-2),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-2),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-30),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },

            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-90),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-1),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-50),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-5),
                IsRead = false
            },

            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Hey, did you get milk yet?",
                SentAt = now.AddHours(-5),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Not yet, going after work.",
                SentAt = now.AddHours(-4),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = john.Id,
                Content = "Can you also grab eggs? We are, once again, completely out.",
                SentAt = now.AddMinutes(-40),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Eggs and milk, got it.",
                SentAt = now.AddMinutes(-18),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = jane.Id,
                Content = "Anything else while I'm there?",
                SentAt = now.AddMinutes(-12),
                IsRead = false
            },




            // Jane <-> Kate: three unread for Jane
            new DirectMessage
            {
                SenderId = kate.Id,
                ReceiverId = jane.Id,
                Content = "Have you finished The Song of Achilles?",
                SentAt = now.AddHours(-6),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = kate.Id,
                ReceiverId = jane.Id,
                Content = "I'm about fifty pages from the end.",
                SentAt = now.AddHours(-5),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = kate.Id,
                ReceiverId = jane.Id,
                Content = "Trying not to cry on the bus.",
                SentAt = now.AddMinutes(-8),
                IsRead = false
            },

            // Jane <-> Alex: two unread for Jane
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = alex.Id,
                Content = "How was deadlift day?",
                SentAt = now.AddDays(-1),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = alex.Id,
                ReceiverId = jane.Id,
                Content = "140kg, finally broke the plateau.",
                SentAt = now.AddHours(-3),
                IsRead = false
            },
            new DirectMessage
            {
                SenderId = alex.Id,
                ReceiverId = jane.Id,
                Content = "Coach reckons 150 is doable by summer.",
                SentAt = now.AddHours(-2),
                IsRead = false
            },

            // Jane <-> Zoe: one unread for Jane
            new DirectMessage
            {
                SenderId = zoe.Id,
                ReceiverId = jane.Id,
                Content = "Finished the feature branch. Tests all green.",
                SentAt = now.AddHours(-7),
                IsRead = false
            },

            // Jane <-> Sam: read, last from Jane
            new DirectMessage
            {
                SenderId = sam.Id,
                ReceiverId = jane.Id,
                Content = "Waffles says hi.",
                SentAt = now.AddDays(-2),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = sam.Id,
                Content = "Bring him over when I mow the lawns.",
                SentAt = now.AddDays(-2).AddHours(1),
                IsRead = true
            },

            // Jane <-> Maria: last from Jane, unread for Maria
            new DirectMessage
            {
                SenderId = maria.Id,
                ReceiverId = jane.Id,
                Content = "Third coffee of the day and it's only 2pm. Send help.",
                SentAt = now.AddHours(-9),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = jane.Id,
                ReceiverId = maria.Id,
                Content = "Classic finals week. You've got this.",
                SentAt = now.AddHours(-8),
                IsRead = false
            },

            // John <-> Alex: latest is unread for John
            new DirectMessage
            {
                SenderId = alex.Id,
                ReceiverId = john.Id,
                Content = "You still coming for the Port Hills hike?",
                SentAt = now.AddDays(-2),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = alex.Id,
                Content = "Yeah I'm in, just tell me when.",
                SentAt = now.AddDays(-2).AddMinutes(20),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = alex.Id,
                ReceiverId = john.Id,
                Content = "Sick, I'll send the time later this arvo.",
                SentAt = now.AddHours(-1),
                IsRead = false
            },

            // John <-> Sam: last message is from John, no unread
            new DirectMessage
            {
                SenderId = sam.Id,
                ReceiverId = john.Id,
                Content = "Waffles knocked another mug off the bench. No regrets.",
                SentAt = now.AddDays(-1),
                IsRead = true
            },
            new DirectMessage
            {
                SenderId = john.Id,
                ReceiverId = sam.Id,
                Content = "Send pics immediately.",
                SentAt = now.AddHours(-20),
                IsRead = true
            }
        );
        db.SaveChanges();
    }
}
