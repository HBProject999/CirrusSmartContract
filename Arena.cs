using Stratis.SmartContracts;
using System;
using System.Text;

/// <summary>
/// A Stratis smart contract for running a game battle where owner will start the battle and maximum 4 users can enter a battle
/// </summary>
public class Arena : SmartContract
{
    public Arena(ISmartContractState smartContractState)
    : base(smartContractState)
    {
        BattleOwner = Message.Sender;
    }

    /// <summary>
    /// Set the address deploying the contract as battle owner
    /// </summary>
    private Address BattleOwner
    {
        get => PersistentState.GetAddress(nameof(BattleOwner));
        set => PersistentState.SetAddress(nameof(BattleOwner), value);
    }

    /// <summary>
    /// Battle owner will start the battle
    /// </summary>
    public bool Start(long battleId, uint maxUsers, ulong price)
    {
        Assert(Message.Sender == BattleOwner, "Only battle owner can start game.");

        var battle = new BattleMain();
        battle.BattleId = battleId;
        battle.MaxUsers = maxUsers;
        battle.Price = price;
        battle.Users = new Address[maxUsers];
        SetBattle(battleId, battle);

        return true;
    }

    /// <summary>
    /// 4 different user will enter the battle
    /// </summary>
    public bool EnterGame(long battleId, int userindex)
    {
        var battle = GetBattle(battleId);

        Assert(battle.WinnerAddress == null || battle.WinnerAddress == Address.Zero, "Battle ended.");

        var user = GetUser(battleId, Message.Sender);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        user.Address = Message.Sender;

        SetUser(battleId, Message.Sender, user);

        battle.Users.SetValue(user.Address, userindex);
        SetBattle(battleId, battle);

        return true;
    }

    /// <summary>
    /// 4 different user will end the battle and submit the score
    /// </summary>
    public bool EndGame(Address userAddress, long battleId, int score)
    {
        Assert(Message.Sender == BattleOwner, "Only battle owner can end game.");

        var battle = GetBattle(battleId);

        Assert(battle.WinnerAddress == null || battle.WinnerAddress == Address.Zero, "Battle ended.");

        var user = GetUser(battleId, userAddress);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        user.Score = score;
        user.ScoreSubmitted = true;

        SetUser(battleId, userAddress, user);

        ProcessWinner(battle);

        return true;
    }

    /// <summary>
    /// Get winner address
    /// </summary>
    public Address GetWinner(long battleId)
    {
        Assert(Message.Sender == BattleOwner, "Only battle owner can give right to play.");
        var battle = GetBattle(battleId);
        return battle.WinnerAddress;
    }

    /// <summary>
    /// Process winner when all user scores are submitted
    /// </summary>
    private void ProcessWinner(BattleMain battle)
    {
        bool AllScoresSubmitted = true;
        foreach (Address userAddress in battle.Users)
        {
            var user = GetUser(battle.BattleId, userAddress);
            if (AllScoresSubmitted && !user.ScoreSubmitted)
                AllScoresSubmitted = false;
        }
        if (AllScoresSubmitted)
        {
            int winnerIndex = GetWinnerIndex(battle.BattleId, battle.Users);
            if (battle.WinnerAddress == null || battle.WinnerAddress == Address.Zero)
            {
                battle.WinnerAddress = battle.Users[winnerIndex];
                SetBattle(battle.BattleId, battle);
                ProcessPrize(battle.BattleId);
            }
        }
    }

    /// <summary>
    /// Get winner user index from battle users
    /// </summary>
    private int GetWinnerIndex(long battleid, Address[] users)
    {
        int winningScore = 0;
        int winningScoreIndex = 0;
        for (int i = 0; i < users.Length; i++)
        {
            var user = GetUser(battleid, users[i]);
            if (user.Score > winningScore)
            {
                winningScore = user.Score;
                winningScoreIndex = i;
            }
        }
        return winningScoreIndex;
    }

    /// <summary>
    /// Send 3/4 amount to winner and 1/4 amount to battle owner
    /// </summary>
    private void ProcessPrize(long battleid)
    {
        var battle = GetBattle(battleid);
        ulong prize = battle.Price * (battle.MaxUsers - 1);
        Transfer(battle.WinnerAddress, prize);
        Transfer(BattleOwner, battle.Price);
    }

    private void SetUser(long battleid, Address address, BattleUser user)
    {
        PersistentState.SetStruct($"user:{battleid}{address}", user);
    }

    private BattleUser GetUser(long battleid, Address address)
    {
        return PersistentState.GetStruct<BattleUser>($"user:{battleid}{address}");
    }

    private void SetBattle(long battleid, BattleMain battle)
    {
        PersistentState.SetStruct($"battle:{battleid}", battle);
    }

    private BattleMain GetBattle(long battleid)
    {
        return PersistentState.GetStruct<BattleMain>($"battle:{battleid}");
    }

    public struct BattleMain
    {
        public long BattleId;
        public Address WinnerAddress;
        public Address[] Users;
        public uint MaxUsers;
        public ulong Price;
    }

    public struct BattleUser
    {
        public Address Address;
        public int Score;
        public bool ScoreSubmitted;
    }

    public struct BattleLog
    {
        public string Message;
    }
}