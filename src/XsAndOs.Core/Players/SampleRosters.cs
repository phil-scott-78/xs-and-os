namespace XsAndOs.Core;

/// <summary>
/// Fixed, deterministic default rosters: a league-average unit with a few standouts
/// (fast WR1, smart QB, mauler LT / weak RG) so attribute sensitivity is visible in tests.
/// </summary>
public static class SampleRosters
{
    public static Team CreateOffense() => new("Home", new[]
    {
        Make("QB1", "Dak Cannon", PlayerPosition.QB, spd: 55, acc: 55, agi: 60, str: 50, awr: 85, cat: 40, pow: 80, thr: 82, blk: 20, tkl: 20),
        Make("RB1", "Buck Miles", PlayerPosition.RB, spd: 85, acc: 88, agi: 85, str: 65, awr: 60, cat: 65, pow: 10, thr: 10, blk: 45, tkl: 20),
        Make("WR1", "Flash Reed", PlayerPosition.WR, spd: 93, acc: 90, agi: 88, str: 45, awr: 65, cat: 82, pow: 10, thr: 10, blk: 30, tkl: 20),
        Make("WR2", "Sy Roberts", PlayerPosition.WR, spd: 84, acc: 82, agi: 80, str: 48, awr: 60, cat: 78, pow: 10, thr: 10, blk: 30, tkl: 20),
        Make("WR3", "Moe Slotte", PlayerPosition.WR, spd: 80, acc: 85, agi: 86, str: 45, awr: 70, cat: 80, pow: 10, thr: 10, blk: 30, tkl: 20),
        Make("TE1", "Gus Bracket", PlayerPosition.TE, spd: 70, acc: 68, agi: 62, str: 75, awr: 65, cat: 72, pow: 10, thr: 10, blk: 68, tkl: 20),
        Make("LT", "Big Ox Hall", PlayerPosition.OL, spd: 45, acc: 45, agi: 45, str: 92, awr: 70, cat: 20, pow: 10, thr: 10, blk: 90, tkl: 20),
        Make("LG", "Sam Girder", PlayerPosition.OL, spd: 42, acc: 42, agi: 40, str: 78, awr: 60, cat: 20, pow: 10, thr: 10, blk: 74, tkl: 20),
        Make("C", "Hub Snapp", PlayerPosition.OL, spd: 42, acc: 42, agi: 42, str: 75, awr: 75, cat: 20, pow: 10, thr: 10, blk: 76, tkl: 20),
        Make("RG", "Lou Turnstile", PlayerPosition.OL, spd: 40, acc: 40, agi: 38, str: 68, awr: 50, cat: 20, pow: 10, thr: 10, blk: 60, tkl: 20),
        Make("RT", "Rex Post", PlayerPosition.OL, spd: 44, acc: 44, agi: 43, str: 80, awr: 62, cat: 20, pow: 10, thr: 10, blk: 78, tkl: 20),
    });

    public static Team CreateDefense() => new("Away", new[]
    {
        Make("DE1", "Edge Kowalski", PlayerPosition.DL, spd: 70, acc: 74, agi: 70, str: 82, awr: 65, cat: 25, pow: 10, thr: 10, blk: 20, tkl: 80),
        Make("DT1", "Tank Molasky", PlayerPosition.DL, spd: 50, acc: 52, agi: 45, str: 90, awr: 60, cat: 20, pow: 10, thr: 10, blk: 20, tkl: 78),
        Make("DT2", "Sal Plugg", PlayerPosition.DL, spd: 48, acc: 50, agi: 44, str: 84, awr: 55, cat: 20, pow: 10, thr: 10, blk: 20, tkl: 74),
        Make("DE2", "Rip Snyder", PlayerPosition.DL, spd: 72, acc: 76, agi: 72, str: 78, awr: 62, cat: 25, pow: 10, thr: 10, blk: 20, tkl: 78),
        Make("LB1", "Mac Trucker", PlayerPosition.LB, spd: 74, acc: 74, agi: 70, str: 76, awr: 78, cat: 45, pow: 10, thr: 10, blk: 20, tkl: 85),
        Make("LB2", "Sid Downhill", PlayerPosition.LB, spd: 76, acc: 75, agi: 72, str: 72, awr: 68, cat: 48, pow: 10, thr: 10, blk: 20, tkl: 80),
        Make("LB3", "Ty Scrape", PlayerPosition.LB, spd: 75, acc: 73, agi: 71, str: 70, awr: 64, cat: 46, pow: 10, thr: 10, blk: 20, tkl: 78),
        Make("CB1", "Ace Shadow", PlayerPosition.CB, spd: 90, acc: 88, agi: 88, str: 45, awr: 72, cat: 60, pow: 10, thr: 10, blk: 15, tkl: 55),
        Make("CB2", "Deuce Latch", PlayerPosition.CB, spd: 86, acc: 85, agi: 84, str: 45, awr: 62, cat: 55, pow: 10, thr: 10, blk: 15, tkl: 55),
        Make("CB3", "Trey Nickels", PlayerPosition.CB, spd: 84, acc: 84, agi: 83, str: 44, awr: 60, cat: 52, pow: 10, thr: 10, blk: 15, tkl: 50),
        Make("FS", "Hawk Deepwater", PlayerPosition.S, spd: 87, acc: 84, agi: 82, str: 55, awr: 80, cat: 62, pow: 10, thr: 10, blk: 15, tkl: 65),
        Make("SS", "Bo Boxx", PlayerPosition.S, spd: 82, acc: 80, agi: 78, str: 68, awr: 70, cat: 55, pow: 10, thr: 10, blk: 15, tkl: 75),
    });

    private static PlayerInfo Make(string id, string name, PlayerPosition pos,
        int spd, int acc, int agi, int str, int awr, int cat, int pow, int thr, int blk, int tkl) =>
        new(id, name, pos, new PlayerAttributes(spd, acc, agi, str, awr, cat, pow, thr, blk, tkl));
}
