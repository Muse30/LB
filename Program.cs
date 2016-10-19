#region

using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using SharpDX;
using Color = System.Drawing.Color;


#endregion

namespace LeBlanc
{
    internal class Program
    {

        public static Spell.Targeted _q;
        public static Spell.Skillshot _w;
        public static Spell.Targeted _w2;
        public static Spell.Skillshot _e;
        public static Spell.Targeted _r;

        public static bool comboFinished = true;
        
        public static SpellSlot ignite;

        private static int LastTick;

        public static AIHeroClient Player
        {
            get { return ObjectManager.Player; }
        }

        private static Menu Menu;
        public static Menu comboMenu, harassMenu, miscMenu, drawMenu, laneMenu, ksMenu, fleeMenu;

        private static void Main(string[] args)
        {
            Loading.OnLoadingComplete += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.BaseSkinName != "Leblanc") return;

            _q = new Spell.Targeted(SpellSlot.Q, 720);
            _w = new Spell.Skillshot(SpellSlot.W, 760, SkillShotType.Circular, int.MaxValue, 1450, 220);
            _w2 = new Spell.Targeted(SpellSlot.W, 1300);
            _e = new Spell.Skillshot(SpellSlot.E, 900, SkillShotType.Linear, 0, 1650, 55);
            _r = new Spell.Targeted(SpellSlot.R, 720);
            ignite = ObjectManager.Player.GetSpellSlotFromName("summonerdot");

            Menu = MainMenu.AddMenu("LeBlanc", "LeBlanc");

            comboMenu = Menu.AddSubMenu("Combo", "Combo");
            comboMenu.Add("combomode", new ComboBox("Combo Mode", 1, "Auto", "Q + R", "W + R", "E + R"));
            comboMenu.AddSeparator();
            comboMenu.Add("wback", new CheckBox("W back (use W2)", false));
            comboMenu.AddSeparator();
            comboMenu.Add("hitchance", new ComboBox("E HitChance", 2, "Low", "Medium", "High", "Dashing", "Immobile"));
            comboMenu.AddSeparator();
            comboMenu.Add("2chainzz", new KeyBind("2 Chainz (E + R)", false, KeyBind.BindTypes.HoldActive, 'L'));
            comboMenu.Add("priority", new KeyBind("Change Priority", false, KeyBind.BindTypes.HoldActive, 'T'));
            comboMenu.AddSeparator();
            comboMenu.AddGroupLabel("Gapclose Combo");
            comboMenu.AddLabel("This will use W to get closer then do Q + R + E");
            comboMenu.Add("wqre", new KeyBind("W (Q+R+E) combo", false, KeyBind.BindTypes.HoldActive, 'Y'));
            comboMenu.Add("wenemies", new Slider("Enemies to do gapclose combo", 1, 1, 5));

           

            harassMenu = Menu.AddSubMenu("Harass", "Harass");
            harassMenu.Add("harassq", new CheckBox("Use Q", true));
            harassMenu.Add("harassw", new CheckBox("Use W", false));
            harassMenu.Add("harasse", new CheckBox("Use E", false));
            harassMenu.Add("harassq1", new Slider("Q Mana", 70, 0, 100));
            harassMenu.Add("harassw1", new Slider("W Mana", 70, 0, 100));
            harassMenu.Add("harasse1", new Slider("E Mana", 70, 0, 100));


            miscMenu = Menu.AddSubMenu("Misc", "Misc");
            miscMenu.Add("egapclose", new CheckBox("use E on Gapclosers"));
            miscMenu.Add("interrupt", new CheckBox("Interrupt Spells"));


            laneMenu = Menu.AddSubMenu("LaneClear", "LaneClear");
            laneMenu.Add("lanew", new CheckBox("Use W"));
            laneMenu.Add("lanemana", new Slider("Lane Mana % ", 50, 0, 100));
            laneMenu.Add("minw", new Slider("Min minions for W ", 3, 1, 5));


            ksMenu = Menu.AddSubMenu("Killsteal", "Killsteal");
            ksMenu.Add("ksq", new CheckBox("Use Q", false));
            ksMenu.Add("ksw", new CheckBox("Use W", false));
            ksMenu.Add("kse", new CheckBox("Use E", false));
            ksMenu.AddSeparator();
            ksMenu.Add("ksqr", new CheckBox("Use R + Q", false));
            ksMenu.Add("kswr", new CheckBox("Use W + R", false));
            ksMenu.Add("kser", new CheckBox("Use R + E", false));


            drawMenu = Menu.AddSubMenu("Drawings", "Drawings");
            drawMenu.Add("showcombo", new CheckBox("Show Combo Mode"));
            drawMenu.Add("drawQ", new CheckBox("Q Range", true));
            drawMenu.Add("drawW", new CheckBox("W Range", true));
            drawMenu.Add("drawE", new CheckBox("E Range", true));


            fleeMenu = Menu.AddSubMenu("Flee", "Flee");
            fleeMenu.Add("fleew", new CheckBox("W Flee", true));
            fleeMenu.Add("fleer", new CheckBox("R Flee", true));


            Game.OnTick += Game_OnTick;
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Game_OnDraw;
            Interrupter.OnInterruptableSpell += OnInterruptableSpell;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Gapcloser.OnGapcloser += OnGapcloser;

        }

        public static bool lbw
        {
            get
            {
                return !_w.IsReady() || ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).Name == "leblancslidereturn";
            }
        }


        public static bool Emark(Obj_AI_Base target)
        {
            return target.HasBuff("LeBlancMarkOfSilence") || target.HasBuff("LeBlancMarkOfSilenceM");
        }

        private static bool enemyhavesoulshackle(Obj_AI_Base vTarget)
        {
            return (vTarget.HasBuff("LeblancSoulShackle"));
        }

        private static Obj_AI_Base enemysoulshackle
        {
            get
            {
                return
                    (from hero in
                        ObjectManager.Get<Obj_AI_Base>().Where(hero => ObjectManager.Player.Distance(hero) <= 1100)
                     where hero.IsEnemy
                     from buff in hero.Buffs
                     where buff.Name.Contains("LeblancSoulShackle")
                     select hero).FirstOrDefault();
            }
        }

        public static Item Fqc = new Item(3092, 750);
        private static void UserSummoners(Obj_AI_Base t)
        {
            if (Fqc.IsReady())
                Fqc.Cast(t.ServerPosition);
        }

        private static ComboType LBcomboType = ComboType.qr;
        private static ComboKillable LBcomboKillable = ComboKillable.OneShot;

        private static float GetRQDamage
        {
            get
            {
                var xDmg = 0f;
                var perDmg = new[] { 100f, 200f, 300 };

                xDmg += ((ObjectManager.Player.BaseAbilityDamage + ObjectManager.Player.FlatMagicDamageMod) * .65f) +
                        perDmg[_r.Level - 1];
                var t = TargetSelector.GetTarget(2000, DamageType.Magical);
                if (t.IsValidTarget(2000))
                    if (LBcomboType == ComboType.qr)
                    {
                        xDmg += QDamage(t);
                    }
                if (LBcomboType != ComboType.qr)
                {
                    xDmg += EDamage(t);
                }
                return xDmg;
            }
        }

        private static float GetRWDamage
        {
            get
            {
                var xDmg = 0f;
                var perDmg = new[] { 150f, 300f, 450f };
                xDmg += ((ObjectManager.Player.BaseAbilityDamage + ObjectManager.Player.FlatMagicDamageMod) * .98f) +
                        perDmg[_r.Level - 1];

                var t = TargetSelector.GetTarget(2000, DamageType.Magical);
                if (t.IsValidTarget(2000))
                    xDmg += WDamage(t);

                return xDmg;
            }
        }

        private static float GetComboDamage(Obj_AI_Base t)
        {
            var fComboDamage = 0f;

            if (!t.IsValidTarget(2000))
                return 0f;

            fComboDamage += _q.IsReady() ? QDamage(t) : 0;

            fComboDamage += _w.IsReady() ? WDamage(t) : 0;

            fComboDamage += _e.IsReady() ? EDamage(t) : 0;

            if (_r.IsReady())
            {
                if (LBcomboType == ComboType.qr || LBcomboType == ComboType.ComboER)
                {
                    fComboDamage += GetRQDamage;
                }

                if (LBcomboType == ComboType.ComboWR)
                {
                    fComboDamage += GetRWDamage;
                }
            }

            fComboDamage += ignite != SpellSlot.Unknown &&
                            ObjectManager.Player.Spellbook.CanUseSpell(ignite) == SpellState.Ready
                ? ObjectManager.Player.GetSummonerSpellDamage(t, DamageLibrary.SummonerSpells.Ignite)
                : 0f;

            fComboDamage += Item.CanUseItem(3092)
                ? ObjectManager.Player.GetItemDamage(t, ItemId.Frost_Queens_Claim)
                : 0;

            return fComboDamage;
        }

        public static float QDamage(Obj_AI_Base target)
        {
            return ObjectManager.Player.CalculateDamageOnUnit(target, DamageType.Magical,
                new[] { 55, 80, 105, 130, 155 }[_q.Level - 1] +
                0.4f * ObjectManager.Player.FlatMagicDamageMod);
        }

        public static float WDamage(Obj_AI_Base target)
        {
            return ObjectManager.Player.CalculateDamageOnUnit(target, DamageType.Magical,
                new[] { 85, 125, 165, 205, 245 }[_w.Level - 1] +
                0.6f * ObjectManager.Player.FlatMagicDamageMod);
        }

        public static float EDamage(Obj_AI_Base target)
        {
            return ObjectManager.Player.CalculateDamageOnUnit(target, DamageType.Magical,
                new[] { 40, 65, 90, 115, 140 }[_e.Level - 1] +
                0.5f * ObjectManager.Player.FlatMagicDamageMod);
        }

        public static float E2Damage(Obj_AI_Base target)
        {
            return ObjectManager.Player.CalculateDamageOnUnit(target, DamageType.Magical,
                new[] { 40, 65, 90, 115, 140 }[_e.Level - 1] +
                0.5f * ObjectManager.Player.FlatMagicDamageMod);
        }

        public static float RQDamage(Obj_AI_Base target)
        {
            return ObjectManager.Player.CalculateDamageOnUnit(target, DamageType.Magical,
                new[] { 100, 200, 300 }[_r.Level - 1] +
                0.65f * ObjectManager.Player.FlatMagicDamageMod);
        }

        private static void Game_OnTick(EventArgs args)
        {
            if (ksMenu["ksq"].Cast<CheckBox>().CurrentValue && _q.IsReady())
            {
                var QTarget = TargetSelector.GetTarget(_q.Range, DamageType.Magical);
                if (QTarget == null) return;
                if (QTarget.Health <= QDamage(QTarget))
                {
                    _q.Cast(QTarget);
                    return;
                }
            }
            if (ksMenu["ksw"].Cast<CheckBox>().CurrentValue && _w.IsReady() &&
                ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).Name == "LeblancSlide")
            {
                var WTarget = TargetSelector.GetTarget(_w.Range, DamageType.Magical);
                if (WTarget == null) return;
                if (WTarget.Health <= WDamage(WTarget))
                {
                    _w.Cast(WTarget.ServerPosition);
                    return;
                }
            }
            if (ksMenu["kse"].Cast<CheckBox>().CurrentValue && _e.IsReady())
            {
                var ETarget = TargetSelector.GetTarget(_e.Range, DamageType.Magical);
                if (ETarget == null) return;
                if (ETarget.Health <= EDamage(ETarget))
                {
                    var pred = _e.GetPrediction(ETarget);
                    if (pred.HitChance == Ehitchance)
                    {
                        var predictE = Prediction.Position.PredictLinearMissile(ETarget, _e.Range, 55, 250, 1600, 0);
                        _e.Cast(predictE.CastPosition);
                    }
                    return;
                }
            }
            if (ksMenu["ksqr"].Cast<CheckBox>().CurrentValue && _r.IsReady() &&
                ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancChaosOrbM")
            {
                var QTarget = TargetSelector.GetTarget(_q.Range, DamageType.Magical);
                if (QTarget == null) return;
                if (QTarget.Health <= RQDamage(QTarget))
                {
                    _r.Cast(QTarget);
                    return;
                }
            }
            if (ksMenu["kswr"].Cast<CheckBox>().CurrentValue && _r.IsReady() &&
                ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSlideM")
            {
                var WTarget = TargetSelector.GetTarget(_w.Range, DamageType.Magical);
                if (WTarget == null) return;
                if (WTarget.Health <= WDamage(WTarget))
                {
                    _r.Cast(WTarget.ServerPosition);
                    return;
                }
            }

            if (ksMenu["kswr"].Cast<CheckBox>().CurrentValue && _r.IsReady() && _e.IsReady())
            {
                var WTarget = TargetSelector.GetTarget(_w.Range, DamageType.Magical);
                if (WTarget == null) return;
                if (WTarget.Health <= WDamage(WTarget) * 2)
                {
                    _w.Cast(WTarget.ServerPosition);
                    if (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSlideM")
                    {
                        _r.Cast(WTarget.ServerPosition);
                    }
                    return;
                }
            }
            if (ksMenu["kser"].Cast<CheckBox>().CurrentValue && _r.IsReady() &&
                ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSoulShackleM")
            {
                var ETarget = TargetSelector.GetTarget(_e.Range, DamageType.Magical);
                if (ETarget == null) return;
                if (ETarget.Health <= ObjectManager.Player.GetSpellDamage(ETarget, SpellSlot.E))
                {
                    var pred = _e.GetPrediction(ETarget);
                    if (pred.HitChance == HitChance.Low)
                    {
                        _r.Cast(ETarget);
                    }
                }
            }
        }

        private static bool drawsoulshackle
        {
            get
            {
                return
                    (from hero in
                        ObjectManager.Get<Obj_AI_Base>().Where(hero => ObjectManager.Player.Distance(hero) <= 1100)
                     where hero.IsEnemy
                     from buff in hero.Buffs
                     select (buff.Name.Contains("LeblancSoulShackle"))).FirstOrDefault();
            }
        }
    
        private enum ComboType
        {
            Auto,
            qr,
            ComboWR,
            ComboER
        }

        private enum ComboKillable
        {
            None,
            OneShot,
            WithoutW
        }

        private static HitChance Ehitchance
        {
            get
            {
                HitChance hitChance;
                var eHitChance = comboMenu["hitchance"].Cast<ComboBox>().CurrentValue;
                switch (eHitChance)
                {
                    case 0:
                        {
                            hitChance = HitChance.Low;
                            break;
                        }
                    case 1:
                        {
                            hitChance = HitChance.Medium;
                            break;
                        }
                    case 2:
                        {
                            hitChance = HitChance.High;
                            break;
                        }
                    case 3:
                        {
                            hitChance = HitChance.Dashing;
                            break;
                        }
                    case 4:
                        {
                            hitChance = HitChance.Immobile;
                            break;
                        }
                    default:
                        {
                            hitChance = HitChance.High;
                            break;
                        }
                }
                return hitChance;
            }
        }

        private static void OnGapcloser(AIHeroClient sender, Gapcloser.GapcloserEventArgs gapcloser)
        {
            if (gapcloser.Sender.IsValidTarget(300f) && _e.IsReady() && miscMenu["egapclose"].Cast<CheckBox>().CurrentValue)
            {
                _e.Cast(ObjectManager.Player);
            }

        }

        private static void ChangePrio()
        {
            var changetime = Environment.TickCount - LastTick;


            if (comboMenu["priority"].Cast<KeyBind>().CurrentValue)
            {
                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 0 && LastTick + 400 < Environment.TickCount)
                {
                    LastTick = Environment.TickCount;
                    comboMenu["combomode"].Cast<ComboBox>().CurrentValue = 1;
                }

                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 1 && LastTick + 400 < Environment.TickCount)
                {
                    LastTick = Environment.TickCount;
                    comboMenu["combomode"].Cast<ComboBox>().CurrentValue = 2;
                }
                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 2 && LastTick + 400 < Environment.TickCount)
                {
                    LastTick = Environment.TickCount;
                    comboMenu["combomode"].Cast<ComboBox>().CurrentValue = 3;
                }
                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 3 && LastTick + 400 < Environment.TickCount)
                {
                    LastTick = Environment.TickCount;
                    comboMenu["combomode"].Cast<ComboBox>().CurrentValue = 0;
                }

            }
        }

        private static void LaneClear()
        {
            if (Player.ManaPercent > laneMenu["lanemana"].Cast<Slider>().CurrentValue)
            {
                if (laneMenu["lanew"].Cast<CheckBox>().CurrentValue)
            {
                var minions = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy, Player.ServerPosition, _w.Range);
                if (minions != null)
                {
                    var Wminions = EntityManager.MinionsAndMonsters.GetCircularFarmLocation(minions, _w.Width, (int)_w.Range);
                    if (laneMenu["minw"].Cast<Slider>().CurrentValue <= Wminions.HitNumber)
                    {
                        _w.Cast(Wminions.CastPosition);
                    }

                }
            }
        }
        }

        private static void Flee()
        {
            EloBuddy.Player.IssueOrder(GameObjectOrder.MoveTo, ObjectManager.Player.Position.Extend(Game.CursorPos, 600).To3D());

            var useW = fleeMenu["fleew"].Cast<CheckBox>().CurrentValue;
            var useR = fleeMenu["fleer"].Cast<CheckBox>().CurrentValue;

            if (useW && _w.IsReady() && !lbw)
                _w.Cast(ObjectManager.Player.Position.Extend(Game.CursorPos, 600).To3D());

            if (useR && _r.IsReady() && ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSlideM")
                _r.Cast(ObjectManager.Player.Position.Extend(Game.CursorPos, 600).To3D());
        }

        private static void Game_OnDraw(EventArgs args)
        {
            if (comboMenu["2chainzz"].Cast<KeyBind>().CurrentValue)
            {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.78f, Color.Red, "Double Stun Active!");
            }

            if (comboMenu["wqre"].Cast<KeyBind>().CurrentValue)
            {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.78f, Color.Red, "Gapclose Combo Active!");
            }
            var t = TargetSelector.GetTarget(_w.Range * 2, DamageType.Physical);
            var xComboText = "Combo Kill";
            if (t.IsValidTarget(_w.Range))
            {
                if (t.Health < GetComboDamage(t))
                {
                    LBcomboKillable = ComboKillable.OneShot;
                    Drawing.DrawText(t.HPBarPosition.X + 145, t.HPBarPosition.Y + 20, Color.Red, xComboText);
                }
            }

            else if (t.IsValidTarget(_w.Range * 2 - 30))
            {
                if (t.Health < GetComboDamage(t) - ObjectManager.Player.GetSpellDamage(t, SpellSlot.W))
                {
                    LBcomboKillable = ComboKillable.WithoutW;
                    xComboText = "Jump + " + xComboText;
                    Drawing.DrawText(t.HPBarPosition.X + 145, t.HPBarPosition.Y + 20, Color.Beige, xComboText);
                }
            }
            
            var xtextx = "You need to be lvl 6 LUL";
            if (Player.Level < 6 && comboMenu["wqre"].Cast<KeyBind>().CurrentValue)
            {
                Drawing.DrawText(Player.HPBarPosition.X + 145, Player.HPBarPosition.Y + 20, Color.LightGreen, xtextx);
            }

            if (drawMenu["drawQ"].Cast<CheckBox>().CurrentValue && _q.IsReady())
            {
                Circle.Draw(SharpDX.Color.Purple, 700, ObjectManager.Player.Position);
            }

            if (drawMenu["drawW"].Cast<CheckBox>().CurrentValue && _w.IsReady())
            {
                Circle.Draw(SharpDX.Color.Purple, 600, ObjectManager.Player.Position);
            }
            if (drawMenu["drawE"].Cast<CheckBox>().CurrentValue && _e.IsReady())
            {
                Circle.Draw(SharpDX.Color.Purple, 950, ObjectManager.Player.Position);
            }

            var heropos = Drawing.WorldToScreen(ObjectManager.Player.Position);

            if (drawMenu["showcombo"].Cast<CheckBox>().CurrentValue)
            {
                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 0)
                {
                    Drawing.DrawText(heropos.X - 15, heropos.Y + 40, System.Drawing.Color.White, "Selected Prio: Auto");
                }
                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 1)
                {
                    Drawing.DrawText(heropos.X - 15, heropos.Y + 40, System.Drawing.Color.White, "Selected Prio: Q + R");
                }
                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 2)
                {
                    Drawing.DrawText(heropos.X - 15, heropos.Y + 40, System.Drawing.Color.White, "Selected Prio: W + R");
                }
                if (comboMenu["combomode"].Cast<ComboBox>().CurrentValue == 3)
                {
                    Drawing.DrawText(heropos.X - 15, heropos.Y + 40, System.Drawing.Color.White, "Selected Prio: E + R");
                }
            }
        }

        private static void RefreshComboType()
        {
            var xCombo = comboMenu["combomode"].Cast<ComboBox>().CurrentValue;
            switch (xCombo)
            {
                case 0:
                    LBcomboType = _q.Level > _w.Level ? ComboType.qr : ComboType.ComboWR;
                    break;
                case 1: //Q-R
                    LBcomboType = ComboType.qr;
                    break;
                case 2: //W-R
                    LBcomboType = ComboType.ComboWR;
                    break;
                case 3: //E-R
                    LBcomboType = ComboType.ComboER;
                    break;
            }
        }

        private static void AOECombo()
        {
            var target = TargetSelector.GetTarget(_w.Range + _q.Range, DamageType.Physical);
            Orbwalker.MoveTo(Game.CursorPos);
            if (_w.IsReady() && target.CountEnemiesInRange(1300) == comboMenu["wenemies"].Cast<Slider>().CurrentValue && !lbw &&
                 Player.Spellbook.GetSpell(SpellSlot.W).Name.ToLower() == "leblancslide")
            {
                _w2.Cast(target.Position);
            }

            if ((Player.Spellbook.GetSpell(SpellSlot.W).Name.ToLower() == "leblancslide" &&
                         !_w.IsReady() ||
                         Player.Spellbook.GetSpell(SpellSlot.W).Name.ToLower() == "leblancslidereturn") &&
                _q.IsReady() && _q.IsInRange(target))
            {
                _q.Cast(target);
            }

            if (_q.IsReady() && _q.IsInRange(target) &&
                Player.Spellbook.GetSpell(SpellSlot.R).Name.ToLower() == "leblancchaosorbm")
            {
                _r.Cast(target);
            }

            if (_e.IsReady() && _e.IsInRange(target) && Emark(target))
            {
                _e.Cast(target);
            }

            if (_e.IsReady() && _e.IsInRange(target) &&
               Emark(target) &&
               Player.Spellbook.GetSpell(SpellSlot.R).Name.ToLower() == "leblancsoulshacklem")
            {
                _e.Cast(target);
            }
        }
        

        private static void Combo()
        {
            var cdQEx = ObjectManager.Player.Spellbook.GetSpell(SpellSlot.Q).CooldownExpires;
            var cdWEx = ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).CooldownExpires;
            var cdEEx = ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).CooldownExpires;

            var cdQ = Game.Time < cdQEx ? cdQEx - Game.Time : 0;
            var cdW = Game.Time < cdWEx ? cdWEx - Game.Time : 0;
            var cdE = Game.Time < cdEEx ? cdEEx - Game.Time : 0;

            var t = TargetSelector.GetTarget(_q.Range * 2, DamageType.Magical);

            if (!t.IsValidTarget())
                return;

            if (LBcomboKillable == ComboKillable.WithoutW && !lbw)
            {
                _w.Cast(t.ServerPosition);
            }

            if (_r.IsReady())
            {
                if (LBcomboType == ComboType.Auto)
                {
                    if (_q.Level > _w.Level)
                    {
                        if (_q.IsReady())
                            ExecuteCombo();
                    }
                    else
                    {
                        if (_w.IsReady())
                            ExecuteCombo();
                    }
                }
                else if ((LBcomboType == ComboType.qr && _q.IsReady()) ||
                         (LBcomboType == ComboType.ComboWR && _w.IsReady()) ||
                         (LBcomboType == ComboType.ComboER && _e.IsReady()))
                    ExecuteCombo();
                else
                {
                    if (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancChaosOrbM") // R-Q
                    {
                        t = TargetSelector.GetTarget(_q.Range, DamageType.Magical);
                        if (t.IsValidTarget(_q.Range) &&
                            t.Health < GetRQDamage + QDamage(t))
                            _r.Cast(t);
                    }
                    if (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSlideM") // R-W
                    {
                        t = TargetSelector.GetTarget(_w.Range, DamageType.Magical);
                        if (t.IsValidTarget(_w.Range) &&
                            t.Health < GetRQDamage + QDamage(t))
                            _r.Cast(t);
                        ObjectManager.Player.Spellbook.CastSpell(SpellSlot.R, t, false);
                    }
                    if (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSoulShackleM") // R-E
                    {
                        t = TargetSelector.GetTarget(_e.Range, DamageType.Magical);
                        if (t.IsValidTarget(_e.Range) &&
                            t.Health < GetRQDamage + QDamage(t))
                        {
                            var pred = _e.GetPrediction(t);
                            if (pred.HitChance >= Ehitchance)
                            {
                                var predictE = Prediction.Position.PredictLinearMissile(t, _e.Range, 55, 250, 1600, 0);
                                _r.Cast(predictE.CastPosition);
                            }
                        }
                    }
                    comboFinished = true;
                }
                return;
            }

            if (_q.IsReady() && t.IsValidTarget(_q.Range) && comboFinished)
            {
                if (LBcomboType == ComboType.qr)
                {
                    if (!_r.IsReady())
                        _q.Cast(t);
                }
                else
                {
                    _q.Cast(t);
                }
            }

            if (_w.IsReady() && t.IsValidTarget(_w.Range) && !lbw && comboFinished)
            {
                if (LBcomboType == ComboType.ComboWR)
                {
                    if (!_r.IsReady())

                        _w.Cast(t);
                }
                else
                {
                    _w.Cast(t);
                }
            }

            if (_e.IsReady() && t.IsValidTarget(_e.Range) && comboFinished)
            {
                if (LBcomboType == ComboType.ComboER)
                {
                    if (!_r.IsReady())
                    {
                        var pred = _e.GetPrediction(t);
                        if (pred.HitChance >= Ehitchance)
                        {
                            var predictE = Prediction.Position.PredictLinearMissile(t, _e.Range, 55, 250, 1600, 0);
                            _e.Cast(predictE.CastPosition);
                        }
                    }
                }
                else
                {
                    var pred = _e.GetPrediction(t);
                    if (pred.HitChance >= Ehitchance && ObjectManager.Player.Distance(t) < _w.Range)
                    {
                        var predictE = Prediction.Position.PredictLinearMissile(t, _e.Range, 55, 250, 1600, 0);
                        _e.Cast(predictE.CastPosition);
                    }
                }
            }

            if (t != null && ignite != SpellSlot.Unknown &&
                ObjectManager.Player.Spellbook.CanUseSpell(ignite) == SpellState.Ready)
            {
                if (ObjectManager.Player.Distance(t) < 650 &&
                    ObjectManager.Player.GetSummonerSpellDamage(t, DamageLibrary.SummonerSpells.Ignite) >= t.Health)
                {
                    ObjectManager.Player.Spellbook.CastSpell(ignite, t);
                }
            }
        }

        private static void ExecuteCombo()
        {
            if (!_e.IsReady())
                return;

            comboFinished = false;

            Obj_AI_Base t;
            var cdQEx = ObjectManager.Player.Spellbook.GetSpell(SpellSlot.Q).CooldownExpires;
            var cdWEx = ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).CooldownExpires;
            var cdEEx = ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).CooldownExpires;

            var cdQ = Game.Time < cdQEx ? cdQEx - Game.Time : 0;
            var cdW = Game.Time < cdWEx ? cdWEx - Game.Time : 0;
            var cdE = Game.Time < cdEEx ? cdEEx - Game.Time : 0;

            if (LBcomboType == ComboType.qr && _q.IsReady())
            {
                t = TargetSelector.GetTarget(_q.Range, DamageType.Magical);
                if (t == null)
                    return;

                _q.Cast(t);
                _r.Cast(t);
            }

            if (LBcomboType == ComboType.ComboWR && _w.IsReady())
            {
                t = TargetSelector.GetTarget(_w.Range, DamageType.Magical);
                if (t == null)
                    return;

                if (!lbw)
                    _w.Cast(t);
                _r.Cast(t);
            }

            if (LBcomboType == ComboType.ComboER && _e.IsReady())
            {
                t = TargetSelector.GetTarget(_e.Range, DamageType.Magical);
                if (t == null)
                    return;

                _e.Cast(t);
                _r.Cast(t);
            }
            comboFinished = true;

            t = TargetSelector.GetTarget(_q.Range, DamageType.Magical);
            UserSummoners(t);
        }

        private static void Harass()
        {
            var qTarget = TargetSelector.GetTarget(_q.Range, DamageType.Magical);
            var wTarget = TargetSelector.GetTarget(_w.Range, DamageType.Magical);
            var eTarget = TargetSelector.GetTarget(_e.Range, DamageType.Magical);

            var useQ = harassMenu["harassq"].Cast<CheckBox>().CurrentValue &&
                       ObjectManager.Player.ManaPercent >= harassMenu["harassq1"].Cast<Slider>().CurrentValue;
            var useW = harassMenu["harassw"].Cast<CheckBox>().CurrentValue &&
                       ObjectManager.Player.ManaPercent >= harassMenu["harassw1"].Cast<Slider>().CurrentValue;
            var useE = harassMenu["harasse"].Cast<CheckBox>().CurrentValue &&
                       ObjectManager.Player.ManaPercent >= harassMenu["harasse1"].Cast<Slider>().CurrentValue;

            if (useQ && qTarget != null && _q.IsReady())
                _q.Cast(qTarget);

            if (useW && wTarget != null && _w.IsReady())
                _w.Cast(wTarget);

            if (useE && eTarget != null && _e.IsReady())
            {
                var pred = _e.GetPrediction(eTarget);
                if (pred.HitChance >= Ehitchance)
                {
                    var predictE = Prediction.Position.PredictLinearMissile(eTarget, _e.Range, 55, 250, 1600, 0);
                    _e.Cast(predictE.CastPosition);
                }
            }
        }

        private static void Chainz()
        {
            EloBuddy.Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);

            if (comboMenu["2chainzz"].Cast<KeyBind>().CurrentValue)
            {
                foreach (var enemy in
                    ObjectManager.Get<Obj_AI_Base>()
                        .Where(
                            enemy =>
                                enemy.IsEnemy && !enemy.IsDead && enemy.IsVisible && !enemy.IsMinion &&
                                ObjectManager.Player.Distance(enemy) < _e.Range + 200 && !enemyhavesoulshackle(enemy)))
                {
                    if (_e.IsReady() && ObjectManager.Player.Distance(enemy) < _e.Range)
                    {
                        var pred = _e.GetPrediction(enemy);
                        if (pred.HitChance >= Ehitchance)
                        {
                            var predictE = Prediction.Position.PredictLinearMissile(enemy, _e.Range, 55, 250, 1600, 0);
                            _e.Cast(predictE.CastPosition);
                        }
                    }
                    else if (_r.IsReady() && ObjectManager.Player.Distance(enemy) < _e.Range &&
                             ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSoulShackleM")
                    {
                        var pred = _e.GetPrediction(enemy);
                        if (pred.HitChance >= Ehitchance)
                        {
                            var predictE = Prediction.Position.PredictLinearMissile(enemy, _e.Range, 55, 250, 1600, 0);
                            _r.Cast(predictE.CastPosition);
                        }
                    }
                }
            }
        }

        private static void OnInterruptableSpell(Obj_AI_Base sender, Interrupter.InterruptableSpellEventArgs args)
        {
            if (!miscMenu["interrupt"].Cast<CheckBox>().CurrentValue)
                return;

            var isValidTarget = sender.IsValidTarget(_e.Range) && args.DangerLevel == DangerLevel.High;

            if (_e.IsReady() && isValidTarget)
            {
                var pred = _e.GetPrediction(sender);
                if (pred.HitChance >= Ehitchance)
                {
                    var predictE = Prediction.Position.PredictLinearMissile(sender, _e.Range, 55, 250, 1600, 0);
                    _e.Cast(predictE.CastPosition);
                }
            }
            else if (_r.IsReady() && ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name == "LeblancSoulShackleM" &&
                     isValidTarget)
            {
                _r.Cast(sender);
            }
        }

        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Flee) || Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo) && !comboMenu["wback"].Cast<CheckBox>().CurrentValue)
            {
                if (args.Slot == SpellSlot.W && ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).Name.Equals("Leblancslidereturn", StringComparison.InvariantCultureIgnoreCase))
                {
                    args.Process = false;
                }

                if (args.Slot == SpellSlot.R && ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Name.ToLower().Equals("Leblancslidereturnm", StringComparison.InvariantCultureIgnoreCase))
                {
                    args.Process = false;
                }
            }

        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (ObjectManager.Player.IsDead)
                return;

            RefreshComboType();

            comboFinished = !_r.IsReady();

            if (comboMenu["2chainzz"].Cast<KeyBind>().CurrentValue)
                Chainz();


            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Flee))
            {
                Flee();
            }

            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
            {
                Combo();
            }

            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear))
            {
                LaneClear();
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Harass))
            {
                Harass();
            }
            if (comboMenu["wqre"].Cast<KeyBind>().CurrentValue && Player.Level >= 6)
            {
                AOECombo();
            }
            ChangePrio();

        }
    }


}