using Lugat.Application.Reminders;

namespace Lugat.Application.Tests.Reminders;

/// <summary>L-94: the reminder's minutes are those of today's share (one review), not of the whole backlog.</summary>
public class ReminderComposeTests
{
    [Fact]
    public void A_few_due_cards_take_their_own_time()
    {
        Assert.Equal("Bugun 3 ta so‘zni takrorlash vaqti. ~1 daqiqa.", ReminderService.Compose(3, 5, 12));
        Assert.Equal("Bugun 30 ta so‘zni takrorlash vaqti. ~10 daqiqa.", ReminderService.Compose(30, 50, 12));
    }

    [Fact]
    public void More_than_one_review_holds_says_the_review_length()
    {
        // 20 minutes at 12 s a card hold 100 cards: 150 due cards still say ~20 minutes, not ~30.
        Assert.Equal("Bugun 90 ta so‘zni takrorlash vaqti. ~20 daqiqa.", ReminderService.Compose(90, 150, 12));
        // A slower learner (30 s a card) gets 40 cards in the same 20 minutes.
        Assert.Equal("Bugun 90 ta so‘zni takrorlash vaqti. ~20 daqiqa.", ReminderService.Compose(90, 150, 30));
    }

    [Fact]
    public void A_backlog_beyond_the_old_200_cap_is_still_one_review()
    {
        // Before L-94: min(500, 200) × 12 s = ~40 minutes.
        Assert.Equal("Bugun 260 ta so‘zni takrorlash vaqti. ~20 daqiqa.", ReminderService.Compose(260, 500, 12));
        Assert.Null(ReminderService.Compose(0, 0, 12));
    }
}
