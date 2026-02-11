using System.Text.Json;

namespace MelmanApp
{
    /// <summary>
    /// AI Bot that learns from previous games and adapts its strategy
    /// </summary>
    public class AIGameBot
    {
        private readonly GameKnowledge _knowledge;
        private readonly StrategyWeights _weights;
        private readonly Dictionary<int, GameState> _activeGames;
        private readonly string _knowledgeFile = "game_knowledge.json";

        public AIGameBot()
        {
            _knowledge = LoadKnowledge();
            _weights = new StrategyWeights();
            _activeGames = new Dictionary<int, GameState>();
        }

        #region Negotiate Endpoint

        public List<object> Negotiate(GameRequest model)
        {
            TrackGameState(model);
            
            var diplomacy = new List<object>();
            
            if (model.EnemyTowers.Count == 0)
                return diplomacy;

            // AI Decision: Who to ally with?
            var allyChoice = DecideAlly(model);
            var attackChoice = DecideNegotiationTarget(model, allyChoice);

            if (allyChoice != null)
            {
                diplomacy.Add(new
                {
                    allyId = allyChoice.PlayerId,
                    attackTargetId = attackChoice?.PlayerId
                });
            }

            return diplomacy;
        }

        #endregion

        #region Combat Endpoint

        public List<GameAction> Combat(GameRequest model)
        {
            TrackGameState(model);
            
            var actions = new List<GameAction>();
            var money = model.PlayerTower.Resources;
            var gameState = _activeGames[model.GameId];

            // AI Decision Tree based on learned weights
            var gamePhase = DetermineGamePhase(model);
            var threatLevel = CalculateThreatLevel(model);

            Console.WriteLine($"[AI] Turn {model.Turn}, Phase: {gamePhase}, Threat: {threatLevel:F2}, Money: {money}");

            // Decision 1: Should we upgrade?
            if (ShouldUpgrade(model, money, gamePhase, threatLevel))
            {
                var upgradeCost = GetUpgradeCost(model.PlayerTower.Level);
                actions.Add(new GameAction { Type = "upgrade" });
                money -= upgradeCost;
                Console.WriteLine($"[AI] Upgrading to level {model.PlayerTower.Level + 1}");
            }

            // Decision 2: How much armor do we need?
            var armorAmount = DecideArmorAmount(model, money, threatLevel, gamePhase);
            if (armorAmount > 0)
            {
                actions.Add(new GameAction
                {
                    Type = "armor",
                    Amount = armorAmount
                });
                money -= armorAmount;
                Console.WriteLine($"[AI] Building {armorAmount} armor");
            }

            // Decision 3: Should we attack and who?
            var attackDecisions = DecideAttacks(model, money, gamePhase, threatLevel);
            actions.AddRange(attackDecisions);

            // Learn from this decision
            gameState.RecordDecision(model.Turn, actions, money);

            return actions;
        }

        #endregion

        #region AI Decision Logic

        private EnemyTower? DecideAlly(GameRequest model)
        {
            // AI learns: Ally with strongest or most aggressive player?
            var allyStrategy = _weights.PreferStrongestAlly;

            if (allyStrategy > 0.5)
            {
                // Ally with strongest (defensive strategy)
                return model.EnemyTowers
                    .OrderByDescending(e => e.Hp + e.Armor + (e.Level * 20))
                    .FirstOrDefault();
            }
            else
            {
                // Ally with most aggressive (offensive strategy)
                var aggressiveEnemy = model.EnemyTowers
                    .OrderByDescending(e => e.Level)
                    .FirstOrDefault();
                return aggressiveEnemy;
            }
        }

        private EnemyTower? DecideNegotiationTarget(GameRequest model, EnemyTower? ally)
        {
            // Target weakest enemy (easiest to eliminate)
            var weakest = model.EnemyTowers
                .Where(e => ally == null || e.PlayerId != ally.PlayerId)
                .OrderBy(e => e.Hp + e.Armor)
                .FirstOrDefault();

            return weakest;
        }

        private bool ShouldUpgrade(GameRequest model, int money, GamePhase phase, double threatLevel)
        {
            var level = model.PlayerTower.Level;
            var upgradeCost = GetUpgradeCost(level);

            if (level >= 5 || money < upgradeCost)
                return false;

            // AI Learning: Adjust upgrade priority based on past performance
            var upgradePriority = _weights.UpgradePriority;
            var turnFactor = model.Turn / 25.0; // 0.0 to 1.0+

            // Early game: Prioritize upgrades more
            if (phase == GamePhase.Early)
            {
                return money >= upgradeCost * (1.0 - upgradePriority * 0.3);
            }

            // Mid game: Balance upgrades with defense/offense
            if (phase == GamePhase.Mid)
            {
                // Don't upgrade if under heavy attack
                if (threatLevel > 0.6)
                    return false;
                
                return money >= upgradeCost * 1.1;
            }

            // Late game: Only upgrade if we have spare resources
            if (phase == GamePhase.Late)
            {
                return money >= upgradeCost * 1.5 && threatLevel < 0.4;
            }

            return false;
        }

        private int DecideArmorAmount(GameRequest model, int money, double threatLevel, GamePhase phase)
        {
            if (money <= 0)
                return 0;

            var incomingDamage = model.PreviousAttacks
                .Where(a => a.Action.TargetId == model.PlayerTower.PlayerId)
                .Sum(a => a.Action.TroopCount);

            var currentArmor = model.PlayerTower.Armor;

            // AI learns optimal armor percentage
            var armorWeight = _weights.DefenseWeight;

            // Under attack: Build more armor
            if (incomingDamage > currentArmor)
            {
                var needed = incomingDamage - currentArmor + (int)(20 * threatLevel);
                var maxSpend = (int)(money * (0.3 + armorWeight * 0.3)); // 30-60% based on learning
                return Math.Min(needed, maxSpend);
            }

            // Proactive armor based on threat and game phase
            if (phase == GamePhase.Early && model.Turn > 3)
            {
                // Minimal armor early
                return Math.Min(10, money / 10);
            }

            if (phase == GamePhase.Mid && currentArmor < 30)
            {
                return Math.Min(15, money / 8);
            }

            if (phase == GamePhase.Late && threatLevel > 0.5)
            {
                // More armor late game if threatened
                return Math.Min(25, (int)(money * 0.25));
            }

            return 0;
        }

        private List<GameAction> DecideAttacks(GameRequest model, int money, GamePhase phase, double threatLevel)
        {
            var attacks = new List<GameAction>();

            if (money <= 10 || model.EnemyTowers.Count == 0)
                return attacks;

            // AI learns when to start attacking
            var aggressionWeight = _weights.AggressionWeight;
            var shouldAttack = false;

            // Decision criteria based on learning
            if (phase == GamePhase.Early)
            {
                // Early game: Only attack if very aggressive OR high level
                shouldAttack = model.PlayerTower.Level >= 3 || 
                               (aggressionWeight > 0.7 && model.Turn >= 5);
            }
            else if (phase == GamePhase.Mid)
            {
                // Mid game: Attack if level 2+ or turn 10+
                shouldAttack = model.PlayerTower.Level >= 2 || model.Turn >= 10;
            }
            else if (phase == GamePhase.Late)
            {
                // Late game: Always attack
                shouldAttack = true;
            }

            if (!shouldAttack)
                return attacks;

            // Choose target
            var target = ChooseAttackTarget(model, aggressionWeight);
            
            if (target != null)
            {
                // Decide how many troops to send
                var troopsToSend = DecideTroopCount(model, money, target, threatLevel, aggressionWeight);
                
                if (troopsToSend > 0)
                {
                    attacks.Add(new GameAction
                    {
                        Type = "attack",
                        TargetId = target.PlayerId,
                        TroopCount = troopsToSend
                    });
                    
                    Console.WriteLine($"[AI] Attacking player {target.PlayerId} with {troopsToSend} troops");
                }
            }

            return attacks;
        }

        private EnemyTower? ChooseAttackTarget(GameRequest model, double aggressionWeight)
        {
            // Check ally requests first
            var allyRequest = model.Diplomacy
                .FirstOrDefault(d => d.Action.AllyId == model.PlayerTower.PlayerId);

            if (allyRequest != null)
            {
                var allyTarget = model.EnemyTowers
                    .FirstOrDefault(e => e.PlayerId == allyRequest.Action.AttackTargetId);
                
                if (allyTarget != null)
                {
                    Console.WriteLine($"[AI] Coordinating with ally to attack {allyTarget.PlayerId}");
                    return allyTarget;
                }
            }

            // AI learns: Attack weakest (finish them) or strongest (reduce threat)?
            if (aggressionWeight > 0.6)
            {
                // High aggression: Eliminate weakest
                return model.EnemyTowers
                    .OrderBy(e => e.Hp + e.Armor)
                    .FirstOrDefault();
            }
            else
            {
                // Lower aggression: Attack who attacked us
                var attackerId = model.PreviousAttacks
                    .Where(a => a.Action.TargetId == model.PlayerTower.PlayerId)
                    .OrderByDescending(a => a.Action.TroopCount)
                    .Select(a => a.PlayerId)
                    .FirstOrDefault();

                var revengeTarget = model.EnemyTowers
                    .FirstOrDefault(e => e.PlayerId == attackerId);

                return revengeTarget ?? model.EnemyTowers
                    .OrderBy(e => e.Hp + e.Armor)
                    .FirstOrDefault();
            }
        }

        private int DecideTroopCount(GameRequest model, int money, EnemyTower target, double threatLevel, double aggression)
        {
            // AI learns optimal troop allocation
            var targetHealth = target.Hp + target.Armor;
            
            // Conservative: Send just enough to damage
            var conservative = Math.Min(targetHealth / 2, money / 2);
            
            // Aggressive: Send most/all resources
            var aggressive = (int)(money * (0.8 + aggression * 0.2));

            // Blend based on threat level and aggression
            if (threatLevel > 0.7)
            {
                // Under threat: Be conservative
                return conservative;
            }
            else if (aggression > 0.7)
            {
                // High aggression: Go all in
                return aggressive;
            }
            else
            {
                // Balanced: Try to eliminate if possible
                if (targetHealth < money)
                    return targetHealth + 10; // Overkill to ensure elimination
                else
                    return (conservative + aggressive) / 2;
            }
        }

        #endregion

        #region Helper Methods

        private GamePhase DetermineGamePhase(GameRequest model)
        {
            if (model.Turn <= 10)
                return GamePhase.Early;
            else if (model.Turn <= 20)
                return GamePhase.Mid;
            else
                return GamePhase.Late;
        }

        private double CalculateThreatLevel(GameRequest model)
        {
            var myHealth = model.PlayerTower.Hp + model.PlayerTower.Armor;
            var myLevel = model.PlayerTower.Level;

            if (model.EnemyTowers.Count == 0)
                return 0.0;

            // Calculate average enemy strength
            var avgEnemyHealth = model.EnemyTowers.Average(e => e.Hp + e.Armor);
            var avgEnemyLevel = model.EnemyTowers.Average(e => e.Level);

            // Calculate incoming damage
            var incomingDamage = model.PreviousAttacks
                .Where(a => a.Action.TargetId == model.PlayerTower.PlayerId)
                .Sum(a => a.Action.TroopCount);

            // Threat factors
            var healthThreat = avgEnemyHealth / Math.Max(myHealth, 1);
            var levelThreat = avgEnemyLevel / Math.Max(myLevel, 1);
            var damageThreat = incomingDamage / Math.Max(myHealth, 50.0);

            return Math.Min(1.0, (healthThreat * 0.3 + levelThreat * 0.3 + damageThreat * 0.4));
        }

        private int GetUpgradeCost(int currentLevel)
        {
            return currentLevel switch
            {
                1 => 50,
                2 => 88,
                3 => 153,
                4 => 268,
                5 => 469,
                _ => int.MaxValue
            };
        }

        private void TrackGameState(GameRequest model)
        {
            if (!_activeGames.ContainsKey(model.GameId))
            {
                _activeGames[model.GameId] = new GameState(model.GameId);
            }

            var state = _activeGames[model.GameId];
            state.UpdateState(model);

            // Check if game ended (we died or won)
            if (model.PlayerTower.Hp <= 0)
            {
                Console.WriteLine($"[AI] Game {model.GameId} ended - We LOST");
                LearnFromGame(state, false);
                _activeGames.Remove(model.GameId);
            }
            else if (model.EnemyTowers.Count == 0)
            {
                Console.WriteLine($"[AI] Game {model.GameId} ended - We WON!");
                LearnFromGame(state, true);
                _activeGames.Remove(model.GameId);
            }
        }

        #endregion

        #region Learning System

        private void LearnFromGame(GameState state, bool won)
        {
            // Record game outcome
            _knowledge.TotalGames++;
            if (won)
                _knowledge.Wins++;
            else
                _knowledge.Losses++;

            // Analyze what worked
            var avgLevel = state.Decisions.Average(d => d.LevelAtTime);
            var avgArmor = state.Decisions.Average(d => d.ArmorBuilt);
            var totalAttacks = state.Decisions.Sum(d => d.AttackCount);
            var firstAttackTurn = state.Decisions.FirstOrDefault(d => d.AttackCount > 0)?.Turn ?? 25;

            Console.WriteLine($"[AI Learning] Game stats - Avg Level: {avgLevel:F1}, Avg Armor: {avgArmor:F0}, Total Attacks: {totalAttacks}, First Attack: Turn {firstAttackTurn}");

            // Adjust weights based on outcome
            if (won)
            {
                // Reinforce successful strategies
                if (avgLevel >= 4)
                    _weights.UpgradePriority = Math.Min(1.0, _weights.UpgradePriority + 0.05);
                
                if (firstAttackTurn <= 10)
                    _weights.AggressionWeight = Math.Min(1.0, _weights.AggressionWeight + 0.05);
                else if (firstAttackTurn >= 15)
                    _weights.AggressionWeight = Math.Max(0.0, _weights.AggressionWeight - 0.03);

                if (avgArmor < 20)
                    _weights.DefenseWeight = Math.Max(0.0, _weights.DefenseWeight - 0.03);
            }
            else
            {
                // Punish failed strategies
                if (avgLevel < 3)
                    _weights.UpgradePriority = Math.Min(1.0, _weights.UpgradePriority + 0.08);
                
                if (state.MaxThreat > 0.8 && avgArmor < 30)
                    _weights.DefenseWeight = Math.Min(1.0, _weights.DefenseWeight + 0.08);
                
                if (totalAttacks < 5)
                    _weights.AggressionWeight = Math.Min(1.0, _weights.AggressionWeight + 0.05);
            }

            _knowledge.RecordGameStats(avgLevel, firstAttackTurn, won);
            SaveKnowledge();

            Console.WriteLine($"[AI Learning] Updated weights - Upgrade: {_weights.UpgradePriority:F2}, Defense: {_weights.DefenseWeight:F2}, Aggression: {_weights.AggressionWeight:F2}");
            Console.WriteLine($"[AI Learning] Win Rate: {_knowledge.WinRate:P0} ({_knowledge.Wins}/{_knowledge.TotalGames})");
        }

        private GameKnowledge LoadKnowledge()
        {
            try
            {
                if (File.Exists(_knowledgeFile))
                {
                    var json = File.ReadAllText(_knowledgeFile);
                    var knowledge = JsonSerializer.Deserialize<GameKnowledge>(json);
                    Console.WriteLine($"[AI] Loaded knowledge from {_knowledgeFile} - {knowledge?.TotalGames ?? 0} games played");
                    return knowledge ?? new GameKnowledge();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Error loading knowledge: {ex.Message}");
            }

            return new GameKnowledge();
        }

        private void SaveKnowledge()
        {
            try
            {
                var json = JsonSerializer.Serialize(_knowledge, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_knowledgeFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Error saving knowledge: {ex.Message}");
            }
        }

        public object GetStats()
        {
            return new
            {
                TotalGames = _knowledge.TotalGames,
                Wins = _knowledge.Wins,
                Losses = _knowledge.Losses,
                WinRate = _knowledge.WinRate,
                Weights = new
                {
                    UpgradePriority = _weights.UpgradePriority,
                    DefenseWeight = _weights.DefenseWeight,
                    AggressionWeight = _weights.AggressionWeight,
                    PreferStrongestAlly = _weights.PreferStrongestAlly
                },
                AverageStats = new
                {
                    AvgLevelAchieved = _knowledge.AvgLevelAchieved,
                    AvgFirstAttackTurn = _knowledge.AvgFirstAttackTurn
                },
                ActiveGames = _activeGames.Count
            };
        }

        public void Reset()
        {
            _weights.Reset();
            _knowledge.Reset();
            _activeGames.Clear();
            SaveKnowledge();
            Console.WriteLine("[AI] Brain reset - all learning data cleared");
        }

        #endregion
    }

    #region Supporting Classes

    public enum GamePhase
    {
        Early,  // Turns 1-10
        Mid,    // Turns 11-20
        Late    // Turns 21+
    }

    public class StrategyWeights
    {
        public double UpgradePriority { get; set; } = 0.7;      // 0.0 to 1.0
        public double DefenseWeight { get; set; } = 0.4;         // 0.0 to 1.0
        public double AggressionWeight { get; set; } = 0.5;      // 0.0 to 1.0
        public double PreferStrongestAlly { get; set; } = 0.7;   // 0.0 to 1.0

        public void Reset()
        {
            UpgradePriority = 0.7;
            DefenseWeight = 0.4;
            AggressionWeight = 0.5;
            PreferStrongestAlly = 0.7;
        }
    }

    public class GameKnowledge
    {
        public int TotalGames { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int Losses { get; set; } = 0;
        public double WinRate => TotalGames > 0 ? (double)Wins / TotalGames : 0.0;
        
        public List<double> LevelsAchieved { get; set; } = new();
        public List<int> FirstAttackTurns { get; set; } = new();
        
        public double AvgLevelAchieved => LevelsAchieved.Count > 0 ? LevelsAchieved.Average() : 0.0;
        public double AvgFirstAttackTurn => FirstAttackTurns.Count > 0 ? FirstAttackTurns.Average() : 0.0;

        public void RecordGameStats(double level, int firstAttackTurn, bool won)
        {
            LevelsAchieved.Add(level);
            FirstAttackTurns.Add(firstAttackTurn);

            // Keep only last 100 games for stats
            if (LevelsAchieved.Count > 100)
                LevelsAchieved.RemoveAt(0);
            if (FirstAttackTurns.Count > 100)
                FirstAttackTurns.RemoveAt(0);
        }

        public void Reset()
        {
            TotalGames = 0;
            Wins = 0;
            Losses = 0;
            LevelsAchieved.Clear();
            FirstAttackTurns.Clear();
        }
    }

    public class GameState
    {
        public int GameId { get; set; }
        public int CurrentTurn { get; set; }
        public double MaxThreat { get; set; } = 0;
        public List<TurnDecision> Decisions { get; set; } = new();

        public GameState(int gameId)
        {
            GameId = gameId;
        }

        public void UpdateState(GameRequest model)
        {
            CurrentTurn = model.Turn;
        }

        public void RecordDecision(int turn, List<GameAction> actions, int resourcesLeft)
        {
            var decision = new TurnDecision
            {
                Turn = turn,
                LevelAtTime = 0,
                ArmorBuilt = actions.FirstOrDefault(a => a.Type == "armor")?.Amount ?? 0,
                AttackCount = actions.Count(a => a.Type == "attack"),
                ResourcesLeft = resourcesLeft
            };

            Decisions.Add(decision);
        }
    }

    public class TurnDecision
    {
        public int Turn { get; set; }
        public int LevelAtTime { get; set; }
        public int ArmorBuilt { get; set; }
        public int AttackCount { get; set; }
        public int ResourcesLeft { get; set; }
    }

    #endregion
}
