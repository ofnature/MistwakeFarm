using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace SealBreaker.Services;

internal static class GrandCompanyState
{
    private static readonly int[] SealCapByRank =
    [
        0,
        10_000,
        15_000,
        20_000,
        25_000,
        30_000,
        35_000,
        40_000,
        45_000,
        50_000,
        80_000,
        90_000,
    ];

    private static readonly string[] RankNames =
    [
        "Unknown",
        "Private Third Class",
        "Private Second Class",
        "Private First Class",
        "Corporal",
        "Sergeant Third Class",
        "Sergeant Second Class",
        "Sergeant First Class",
        "Chief Sergeant",
        "Second Lieutenant",
        "First Lieutenant",
        "Captain",
    ];

    public static bool TryGetDetected(out int grandCompanyIndex, out int rank, out int sealCap)
    {
        grandCompanyIndex = -1;
        rank = 0;
        sealCap = 0;

        unsafe
        {
            var playerState = PlayerState.Instance();
            if (playerState == null)
                return false;

            grandCompanyIndex = GrandCompanyRowToIndex(playerState->GrandCompany);
            rank = (int)playerState->GetGrandCompanyRank();
        }

        if (grandCompanyIndex < 0)
            return false;

        sealCap = SealCapForRank(rank);
        return sealCap > 0;
    }

    public static int SealCapForRank(int rank)
    {
        if (rank >= SealCapByRank.Length)
            return SealCapByRank[^1];

        return rank >= 0 ? SealCapByRank[rank] : 0;
    }

    public static string RankName(int rank)
    {
        if (rank >= RankNames.Length)
            return RankNames[^1];

        return rank >= 0 ? RankNames[rank] : RankNames[0];
    }

    public static string GrandCompanyName(int grandCompanyIndex) => grandCompanyIndex switch
    {
        0 => "Maelstrom",
        1 => "Order of the Twin Adder",
        2 => "Immortal Flames",
        _ => "Unknown",
    };

    private static int GrandCompanyRowToIndex(byte rowId) => rowId switch
    {
        1 => 0,
        2 => 1,
        3 => 2,
        _ => -1,
    };
}
