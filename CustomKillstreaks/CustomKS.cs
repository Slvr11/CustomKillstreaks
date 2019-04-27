using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InfinityScript;
using System.Threading;

namespace UAVTest
{
    public class Killstreaks : BaseScript
    {
        private int Laser_FX;
        private int Crate_FX;
        private int AfterburnerFX;
        private int ContrailFX;
        private int BulletRainFX;
        private int WingTipLight_Green;
        private int WingTipLight_Red;
        public int SentryExplodeFX;
        public int SentrySmokeFX;
        //private string KillstreakCaller;
        private float HeliTime;

        private Entity _airdropCollision;
        //bool GameEnded = false;
        //int missileRemoteLaunchTargetDist = 1500;

        List<Entity> StreaksInAir = new List<Entity>();//Used for aastrike and for Airspace
        //List<string> StreakList = new List<string>();//Used for storing streaks for stacking.
        //private string[] StreakList = new string[];

        public Killstreaks()
            : base()
        {
            Call("precacheitem", "manned_gl_turret_mp");
            AfterburnerFX = Call<int>("loadfx", "fire/jet_afterburner");
            ContrailFX = Call<int>("loadfx", "smoke/jet_contrail");
            BulletRainFX = Call<int>("loadfx", "misc/warthog_volley_runner");
            Laser_FX = Call<int>("loadfx", "misc/laser_glow");
            Crate_FX = Call<int>("loadfx", "smoke/signal_smoke_airdrop");//TODO: find name for Care Package red smoke
            WingTipLight_Green = Call<int>("loadfx", "misc/aircraft_light_wingtip_green");
            WingTipLight_Red = Call<int>("loadfx", "misc/aircraft_light_wingtip_red");
            SentryExplodeFX = Call<int>("loadfx", "explosions/sentry_gun_explosion");
            SentrySmokeFX = Call<int>("loadfx", "smoke/car_damage_blacksmoke");
            Entity care_package = Call<Entity>("getent", "care_package", "targetname");
            _airdropCollision = Call<Entity>("getent", care_package.GetField<string>("target"), "targetname");
            HeliTime = 30;//We'll have to tune this for the stuffz. Harriers and A-10 need this with Blackbox

            PlayerConnected += new Action<Entity>(entity =>
            {
                entity.SetField("StreakList", new Parameter(new List<string>()));
                entity.SetField("killstreak", 0);
                entity.SetField("specialty_falldamage", 0);
                entity.SetField("specialty_paint_pro", 0);
                entity.SetField("JuicedActive", 0);
                entity.SetField("CombatHighActive", 0);
                entity.SetField("HasDiedWithPerks", 0);
                entity.SetField("hasSpecialist", 0);
                entity.SetField("ThermalToggleSet", 0);
                entity.SetField("HasSteadyAimPro", 0);
                entity.SetField("HasSharpFocus", 0);
                entity.SetField("HasTeamPerk", 0);
                entity.SetField("BlackBox", 0);
                entity.SetField("AmmoGotten", 1);//Keeps players from swapping without the streak
                entity.SetField("isCarryingSentry", 0);
                //entity.Call("setclientdvar", "missileRemoteSpeedTargetRange", 750);
                //entity.Call("setclientdvar", "missileRemoteSteerPitchRange", 360);
                //entity.Call("setclientdvar", "missileRemoteSteerPitchRate", 140);
                //entity.Call("setclientdvar", "missileRemoteSteerYawRate", 140);
                //entity.Call("setclientdvar", "missileRemoteSpeedUp", 500);
                PrecacheGameItems();
                //entity.Call("notifyonplayercommand", "cancel_location", "togglemenu");
                entity.Call("notifyonplayercommand", "ToggleThermal", "vote yes");
                entity.OnNotify("ToggleThermal", player =>
                {
                    if (player.GetField<int>("hasSpecialist") == 0) return;
                    if (player.GetField<int>("ThermalOn") == 1)
                    {
                        player.Call("thermalvisionoff");
                        player.SetField("ThermalOn", 0);
                        player.Call("iprintlnbold", "Thermal Off");
                    }
                    else if (player.GetField<int>("ThermalOn") == 0)
                    {
                        player.Call("thermalvisionon");
                        player.SetField("ThermalOn", 1);
                        player.Call("iprintlnbold", "Thermal On");
                    }
                    else return;
                });
                entity.OnNotify("weapon_change", (player, newWeap) =>
                {
                    if (mayDropWeapon((string)newWeap))
                        player.SetField("lastDroppableWeapon", (string)newWeap);
                    KillstreakUseWaiter(player, (string)newWeap);

                    if (player.GetField<int>("AmmoGotten") == 0)
                    {
                        player.Call("givemaxammo", newWeap);
                        player.SetField("AmmoGotten", 1);
                    }
                });

                //entity.Call("notifyonplayercommand", "glswitch", "+actionslot 1");
                //entity.OnNotify("glswitch", (player) =>
                //{
                    //print("glswitch activated");
                    //if (entity.CurrentWeapon == "gl_mp" || !entity.Call<bool>("hasweapon", "gl_mp")) return;
                    //entity.SwitchToWeapon("gl_mp");
                //});

                //entity.Call("notifyonplayercommand", "fly", "+frag");
                entity.OnNotify("fly", (ent) =>
                {
                    if (entity.GetField<string>("sessionstate") != "spectator")
                    {
                        entity.Call("allowspectateteam", "freelook", true);
                        entity.SetField("sessionstate", "spectator");
                        entity.Call("setcontents", 0);
                    }
                    else
                    {
                        entity.Call("allowspectateteam", "freelook", false);
                        entity.SetField("sessionstate", "playing");
                        entity.Call("setcontents", 100);
                    }
                });

                entity.OnNotify("missile_fire", (Entity owner, Parameter ent, Parameter weaponName) =>
                {
                    if ((string)weaponName != "uav_strike_marker_mp")
                        return;
                    Entity missile = ent.As<Entity>();
                    AfterDelay(50, () =>
                                owner.SwitchToWeapon(entity.GetField<string>("lastDroppableWeapon")));
                    entity.AfterDelay(750, (delayer) =>
                            owner.TakeWeapon("uav_strike_marker_mp"));

                    PrintNameInFeed(owner);

                    owner.GetField<List<string>>("StreakList").RemoveAll(RemoveUAV);
                    //SetNextStreakInList(ent);
                    owner.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);

                    missile.OnNotify("death", (g) =>
                    {
                        Call("magicbullet", "uav_strike_projectile_mp", missile.Origin + new Vector3(0, 0, 6000), missile.Origin, owner);
                        Vector3 target = missile.Origin;
                        Entity Effect = Call<Entity>("spawnFx", Laser_FX, target);
                        Call("triggerfx", Effect);
                        Effect.AfterDelay(500, h => Effect.Call("delete"));
                        //missile = null;
                    });
                });
                entity.SpawnedPlayer += () => OnPlayerSpawned(entity);
            });
        }
        
        public void OnPlayerSpawned(Entity player)
        {
            AfterDelay(500, () =>
                SetKillstreakCounter(player));//Delayed to support gamemodes like Weapon Roulette
            player.SetField("HasSOH", player.Call<int>("hasperk", "specialty_fastreload"));
            SetKillstreakCounter(player);
            //StartHUD(player);
        }

        public void PrecacheGameItems()
        {
            Call(327, "compass_objpoint_airstrike_busy");
            Call(327, "compass_objpoint_airstrike_friendly");
            Call(327, "hud_minimap_harrier_red");
            Call(327, "hud_minimap_harrier_green");
            Call(297, "compass_objpoint_airstrike_busy");
            Call(297, "compass_objpoint_airstrike_friendly");
            Call(297, "hud_minimap_harrier_red");
            Call(297, "hud_minimap_harrier_green");
            Call(297, "compass_objpoint_c130_friendly");
            Call(297, "compass_objpoint_c130_enemy");
            Call(327, "compass_objpoint_c130_friendly");
            Call(327, "compass_objpoint_c130_enemy");
            Call(294, "vehicle_av8b_harrier_jet_mp");
            Call(294, "vehicle_av8b_harrier_jet_opfor_mp");
            Call(294, "vehicle_phantom_ray");
            Call(297, "juggernaut_overlay_alpha_col");
            Call(448, "harrier_mp");
            Call(296, "manned_gl_turret_mp");
            Call(328, "MP_A10_strafing_run");
            Call(297, "hint_health");
            Call(297, "weapon_attachment_thermal");
            Call(297, "damage_feedback_lightarmor");
            /*
            Call(297, "specialty_carepackage");
            Call(297, "specialty_predator_missile");
            Call(297, "specialty_precision_airstrike");
            Call(297, "specialty_deployable_vest");
            Call(297, "specialty_remote_mg_turret");
            Call(297, "specialty_airdrop_emergency");
            Call(297, "cardicon_aircraft_01");
            Call(297, "hud_icon_artillery");
            Call(297, "hud_icon_m16a4_grenade");
            Call(297, "viper_ammo_overlay_mp");
            Call(297, "cardicon_award_jets");
            Call(297, "specialty_airdrop_juggernaut");
            Call(297, "iw5_cardicon_juggernaut_a");
            Call(297, "hud_killstreak_bar_empty");
            Call(297, "hud_killstreak_bar_full");
            Call(297, "viper_missile_overlay_mp");
             */
        }

        private static void print(string format, params object[] p)
        {
            Log.Write(LogLevel.All, format, p);
        }

        public override void OnSay(Entity player, string name, string message)
        {
            if (player.Name != "Slvr99") return;
            switch (message)
            {
                case "giveJuggGL":
                    player.SetField("killstreak", 15);
                    CheckStreak(player);
                    break;
                case "giveSuperAirstrike":
                    player.SetField("killstreak", 8);
                    CheckStreak(player);
                    break;
                case "giveTeamAmmoRefill":
                    player.SetField("killstreak", 6);
                    CheckStreak(player);
                    break;
                case "giveA10":
                    player.SetField("killstreak", 11);
                    CheckStreak(player);
                    break;
                case "giveHarriers":
                    player.SetField("killstreak", 7);
                    CheckStreak(player);
                    break;
                case "giveAirSup":
                    player.SetField("killstreak", 12);
                    CheckStreak(player);
                    break;
                case "giveJuggRecon":
                    player.SetField("killstreak", 17);
                    CheckStreak(player);
                    break;
                case "giveMortars":
                    player.SetField("killstreak", 14);
                    CheckStreak(player);
                    break;
                case "giveEmergAirdrop":
                    player.SetField("killstreak", 9);
                    CheckStreak(player);
                    break;
                    case "giveGL":
                    player.SetField("killstreak", 8);
                    CheckStreak(player);
                    break;
                    case "giveSentryGL":
                    player.SetField("killstreak", 10);
                    CheckStreak(player);
                    break;
                case "viewpos":
                    print("({0}, {1}, {2})", player.Origin.X, player.Origin.Y, player.Origin.Z);
                    break;
                case "perks":
                    player.SetField("killstreak", 16);
                    AfterDelay(100, () =>
                    CheckStreak(player));
                    break;
                case "StreaklistTest":
                    List<string> Streaks = player.GetField<List<string>>("StreakList");
                    foreach (string s in Streaks)
                        print(s);
                    break;
                case "List":
                    Streaks = player.GetField<List<string>>("StreakList");
                    try
                    {
                        print(Streaks[0]);
                        print(Streaks[1]);
                    }
                    catch
                    {
                        print("Streak is nonexistant");
                    }
                    break;
            }
            if (message.Contains("setStreak"))
                player.SetField("killstreak", Convert.ToInt32(message.Split(' ')[1]));
        }
        /*
        public void StartHUD(Entity player)
        {
            HudElem nvIcon = HudElem.CreateIcon(player, "hint_health", 20, 20);
            nvIcon.SetPoint("BOTTOM", "BOTTOM", 0, -15);
            nvIcon.HideWhenInMenu = true;
            nvIcon.Foreground = true;
            nvIcon.SetShader("hint_health", 20, 20);
            nvIcon.Alpha = 1;

            Log.Write(LogLevel.All, "KS Icon HUD is {0}", killstreakIcon);

            HudElem[] ksBox = new HudElem[17] { HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player), HudElem.NewClientHudElem(player) };

            foreach (HudElem h in ksBox)
            {
                Log.Write(LogLevel.All, "Set KS Boxhud");
                //h.SetPoint("BOTTOM RIGHT", "BOTTOM RIGHT", 0, -15);
                h.HideWhenInMenu = true;
                h.Alpha = 1;
            }

            player.SetField("ksBoxes", new Parameter(ksBox));
            player.SetField("ksIcon", new Parameter(killstreakIcon));

            OnInterval(500, () =>
                {
                    SetKSBoxes(player);
                    if (player.IsAlive) return true;
                    else return false;
                });
        }

        public int GetBoxX()
        {
            return 0;
        }
        */

        public override void OnPlayerKilled(Entity player, Entity inflictor, Entity attacker, int damage, string mod, string weapon, Vector3 dir, string hitLoc)
        {
            try
            {
                if (attacker.IsAlive && !player.IsAlive)
                {
                    player.SetField("killstreak", 0);
                    attacker.SetField("killstreak", attacker.GetField<int>("killstreak") + 1);
                    AfterDelay(150, () =>
                        {
                            CheckStreak(attacker);
                        });
                }
                else if (!attacker.IsAlive && !player.IsAlive)
                {
                    attacker.SetField("killstreak", 0);
                    player.SetField("killstreak", 0);
                }
                if (player.GetField<int>("HasDiedWithPerks") == 0 && player.GetField<int>("hasSpecialist") == 1)
                {
                    AfterDelay(150, () =>
                        {
                            if (player.GetField<int>("specialty_falldamage") == 1)
                            {
                                player.SetField("specialty_falldamage", 0);
                            }
                            if (player.GetField<int>("ThermalToggleSet") == 1)
                            {
                                var ThrmlImg = player.GetField<HudElem>("ThrmlImg");
                                var ThrmlTxt = player.GetField<HudElem>("ThrmlTxt");
                                DestroyThermalText(player, ThrmlImg, ThrmlTxt);
                            }
                            if (player.GetField<int>("specialty_paint_pro") == 1)
                            {
                                player.SetField("specialty_paint_pro", 0);
                            }
                            if (player.GetField<int>("HasSteadyAimPro") == 1)
                            {
                                player.SetField("HasSteadyAimPro", 0);
                                player.Call("setaimspreadmovementscale", 1.0f);
                            }
                            if (player.GetField<int>("BlackBox") == 1)
                            {
                                HeliTime = 30;
                            }
                            if (player.GetField<int>("JuicedActive") == 1)
                            {
                                player.Call("setmovespeedscale", 1.07f);
                                player.SetField("JuicedActive", 0);
                                var juicedTimer = player.GetField<HudElem>("juicedTimer");
                                var juicedIcon = player.GetField<HudElem>("juicedIcon");
                                RemoveJuicedIcon(player, juicedTimer, juicedIcon);
                            }
                            if (player.GetField<int>("CombatHighActive") == 1)
                            {
                                player.SetField("CombatHighActive", 0);
                                var combatHighTimer = player.GetField<HudElem>("combatHighTimer");
                                var combatHighIcon = player.GetField<HudElem>("combatHighIcon");
                                var combatHighOverlay = player.GetField<HudElem>("combatHighOverlay");
                                RemoveCombatHighIcon(player, combatHighTimer, combatHighIcon, combatHighOverlay);
                            }
                            if (player.GetField<int>("HasSharpFocus") == 1)
                            {
                                player.Call("setviewkickscale", 1);
                                player.SetField("HasSharpFocus", 0);
                            }
                            player.SetField("hasSpecialist", 0);
                        });
                }
            }
            catch
            {
                Log.Write(LogLevel.All, "ERROR IN OnPlayerKilled!");
            }
        }

        public override void OnPlayerDamage(Entity player, Entity inflictor, Entity attacker, int damage, int dFlags, string mod, string weapon, Vector3 point, Vector3 dir, string hitLoc)
        {
            if (player.GetField<int>("specialty_falldamage") == 1)
            {
                if (mod == "MOD_FALLING")
                {
                    player.SetField("health", 100);
                    //damage = 1;//If this doesn't work, use above
                }
            }
            if (attacker == null) return;
            if (attacker != player)
            {
                if (player.GetField<int>("CombatHighActive") == 1)
                {
                    //figure out this code. Might need to work on placement and the way it works. Either use the correct shader or use singular images
                    HudElem combatHighFeedback = HudElem.NewClientHudElem(attacker);
                    combatHighFeedback.HorzAlign = "center";
                    combatHighFeedback.VertAlign = "middle";
                    combatHighFeedback.X = -12;
                    combatHighFeedback.Y = -12;
                    combatHighFeedback.Alpha = 0;
                    combatHighFeedback.Archived = true;
                    combatHighFeedback.SetShader("damage_feedback_lightarmor", 24, 48);
                    combatHighFeedback.Alpha = 1;
                    AfterDelay(1000, () =>
                    {
                        combatHighFeedback.Call<HudElem>("fadeovertime", 1);
                        combatHighFeedback.Alpha = 0;
                    });
                    AfterDelay(2000, () =>
                    {
                        combatHighFeedback.Call<HudElem>("destroy");//Maybe use some other method other than creating a new HudElem every time. May result in frequent crashes
                    });
                }
                if (attacker.GetField<int>("specialty_paint_pro") == 1)
                {
                    player.SetPerk("specialty_radararrow", true, true);
                    player.AfterDelay(10000, (p) =>
                    {
                        p.Call("unsetperk", "specialty_radararrow");
                    });
                }
            }
        }

        private bool CollidingSoon(Entity refobject, Entity player)
        {
            Vector3 endorigin = refobject.Origin + Call<Vector3>("anglestoforward", refobject.GetField<Vector3>("angles")) * 100;

            if (SightTracePassed(refobject.Origin, endorigin, false, player))
                return false;
            else
                return true;
        }
        private bool SightTracePassed(Vector3 StartOrigin, Vector3 EndOrigin, bool tracecharacters, Entity ignoringent)
        {
            int trace = Call<int>("SightTracePassed", new Parameter(StartOrigin), new Parameter(EndOrigin), tracecharacters, new Parameter(ignoringent));
            if (trace > 0)
                return true;
            else
                return false;
        }
        public void PrintNameInFeed(Entity player)
        {
            Call(334, string.Format("UAV Strike called in by {0}", player.GetField<string>("name")));
        }
        private void CheckStreak(Entity player)
        {
            List<string> StreakList = player.GetField<List<string>>("StreakList");
            int Killstreak = player.GetField<int>("killstreak");
            if (Killstreak == 5)//UAV Strike
            {
                player.Call(33392, "uav_strike", 0, Killstreak);
                AfterDelay(2800, () =>
                player.Call(33392, "selected_uav_strike", 0));
                player.Call("giveWeapon", "uav_strike_marker_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "uav_strike_marker_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "nextIndex", 1);
                player.Call(33466, "mp_killstreak_hellfire");
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("predator_missile"));
                StreakList.Add("uav_strike");
            }
            if (player.GetField<int>("killstreak") == -1)//Ammo Airdrop
            {
                //We need to change this. Only call the Ammo thread.

                //player.SetField("customStreak", "ammo");
                
                //player.Call(33392, "ammo", 0);
                //player.Call("giveWeapon", wep, 0, false);
                //player.Call("setActionSlot", 4, "weapon", wep);
                //player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                //player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("ammo"));
            }
            if (Killstreak == 8)//CUSTOM GL, Set to -1 for Cp exclusive only. Avoids obtaining by other means
            {
                player.Call(33392, "thumper", 0, Killstreak);
                AfterDelay(2800, () =>
                player.Call(33392, "selected_gl_turret", 0));
                player.Call("giveWeapon", "killstreak_emp_mp", 0, false);
                player.Call("setActionSlot", 6, "weapon", "killstreak_emp_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 2, true);
                player.Call("SetPlayerData", "killstreaksState", "nextIndex", 3);
                player.Call(33466, "mp_killstreak_mp_killstreak_pavelow");
                //player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("gl_turret"));
                StreakList.Add("gl_turret");
            }
            if (Killstreak == 6)//Team Ammo Refill
            {
                player.Call(33392, "team_ammo_refill", 0, Killstreak);
                AfterDelay(2800, () =>
                player.Call(33392, "selected_team_ammo_refill", 0));
                player.Call("giveWeapon", "killstreak_uav_mp", 0, false);
                player.Call("setActionSlot", 5, "weapon", "killstreak_uav_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 1, true);
                player.Call("SetPlayerData", "killstreaksState", "nextIndex", 2);
                player.Call(33466, "mp_killstreak_carepackage");
                //player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("team_ammo_refill"));
                StreakList.Add("team_ammo_refill");
            }
            //if (player.GetField<int>("killstreak") == 7)//AAMissile
            //{
            //    player.SetField("customStreak", "aamissile");

            //    player.Call(33392, "aamissile", 0);
            //    player.Call("giveWeapon", "uav_strike_projectile_mp", 0, false);
            //    player.Call("setActionSlot", 4, "weapon", "uav_strike_projectile_mp");
            //    player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
            //    player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("aamissile"));
            //}
            if (Killstreak == 16)//CUSTOM Adv. Spec.
            {
                player.Call(33392, "all_perks_bonus", 0, Killstreak);
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 4, true);
                GiveStreaks(player);
                player.Call(33466, "earn_superbonus");
            }
            /*
            if (player.GetField<int>("killstreak") == 8)//Super Airstrike
            {
                player.SetField("customStreak", "super_airstrike");

                player.Call(33392, "super_airstrike", 0);
                player.Call("giveWeapon", "killstreak_triple_uav_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_triple_uav_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("super_airstrike"));
                player.Call(33466, "US_KS_hqr_airstrike");
            }
            if (player.GetField<int>("killstreak") == 11)//A10
            {
                player.Call(33392, "a10_support", 0);
                AfterDelay(2800, () =>
                player.Call(33392, "selected_a10_support", 0));
                player.Call("giveWeapon", "killstreak_remote_tank_laptop_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_remote_tank_laptop_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("a10_support"));
            }
            if (player.GetField<int>("killstreak") == 15)//Jugg GL
            {
                player.SetField("customStreak", "airdrop_juggernaut_gl");

                player.Call(33392, "airdrop_juggernaut_gl", 0);
                player.Call("giveWeapon", "airdrop_juggernaut_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "airdrop_juggernaut_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("airdrop_juggernaut_gl"));
            }
            if (player.GetField<int>("killstreak") == 17)//Jugg Recon
            {
                player.SetField("customStreak", "airdrop_juggernaut_def");

                player.Call(33392, "airdrop_juggernaut_def", 0);
                player.Call("giveWeapon", "airdrop_sentry_marker_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "airdrop_sentry_marker_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("airdrop_juggernaut_def"));
            }
            if (player.GetField<int>("killstreak") == 12)//Air Superiority
            {
                player.SetField("customStreak", "aastrike");

                player.Call(33392, "aastrike", 0);
                player.Call("giveWeapon", "killstreak_helicopter_flares_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_helicopter_flares_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("aastrike"));
            }
            if (player.GetField<int>("killstreak") == 7)//Harriers
            {
                player.Call(33392, "harrier_airstrike", 0, Killstreak);
                player.Call("giveWeapon", "killstreak_predator_missile_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_predator_missile_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("harrier_airstrike"));
                player.Call(33466, "US_1mc_achieve_harriers");
                StreakList.Add("mobile_mortar");
            }
             */
            if (Killstreak == 14)//Mobile Mortars
            {
                player.Call(33392, "mobile_mortar", 0, Killstreak);
                //player.Call(33392, "selected_mobile_mortar", 0);//Doesnt exist
                player.Call("giveWeapon", "killstreak_helicopter_mp", 0, false);
                player.Call("setActionSlot", 7, "weapon", "killstreak_helicopter_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("remote_tank"));
                player.Call(33466, "mp_killstreak_uav");
                //player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("mobile_mortar"));
                StreakList.Add("mobile_mortar");
            }
            /*
            if (player.GetField<int>("killstreak") == 9)//Emergency Airdrop
            {
                player.SetField("customStreak", "airdrop_mega");

                player.Call(33392, "airdrop_mega", 0);
                player.Call("giveWeapon", "airdrop_marker_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "airdrop_marker_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("airdrop_mega"));
                //player.Call(33466, "");
            }
             */
            if (Killstreak == 10)//Sentry GL
            {
                //player.SetField("customStreak", "airdrop_mega");

                player.Call(33392, "sentry_gl", 0);
                player.Call("giveWeapon", "killstreak_sentry_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_sentry_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("sentry"));
                //player.Call(33466, "battlechatter/US/mp/US_1mc_achieve_emerg_airdrop.wav");
                StreakList.Add("sentry_gl");
            }
        }
        private int getKillstreakIndex(string streakName)
        {
            int ret = 0;
            ret = Call<int>("tableLookupRowNum", "mp/killstreakTable.csv", 1, streakName) - 1;

            return ret;
        }
        private void GiveStreaks(Entity player)
        {
        	player.SetField("HasDiedWithPerks", 0);//reset the death check for select perks
            player.SetField("hasSpecialist", 1);

            player.SetPerk("specialty_parabolic", true, true);
            player.SetPerk("specialty_gpsjammer", true, true);
            player.SetPerk("specialty_quieter", true, true);
            player.SetPerk("specialty_longersprint", true, true);
            player.SetPerk("specialty_detectexplosive", true, true);
            player.SetPerk("specialty_bulletaccuracy", true, true);
            player.SetPerk("specialty_bulletaccuracy2", true, true);
            player.SetPerk("specialty_rof", true, true);
            player.SetPerk("specialty_fastreload", true, true);
            player.SetField("HasSOH", 1);//Used for GLCheck
            player.SetPerk("specialty_extraammo", true, true);
            player.SetPerk("specialty_armorvest", true, true);
            player.SetPerk("specialty_burstfire", true, true);
            player.SetField("specialty_falldamage", 1);
            //player.SetField("_specialty_blastshield", 1);//May have to use same mechanic as Explosivedamage but without a check, use OnPlayerDamage
            LocalJammer(player);
            Shield(player);
            player.SetPerk("specialty_jumpdive", true, true);
            player.SetPerk("specialty_fastmantle", true, true);
            Thermal(player);
            //BlackBox(player);
            player.SetPerk("specialty_lightweight", true, true);
            player.SetPerk("specialty_quickdraw", true, true);
            player.SetPerk("specialty_scavenger", true, true);
            player.SetPerk("specialty_amplify", true, true);
            player.SetPerk("specialty_extendedmags", true, true);
            player.SetPerk("specialty_coldblooded", true, true);
            player.SetPerk("specialty_blindeye", true, true);
            player.SetPerk("specialty_marathon", true, true);
            player.SetPerk("specialty_extendedmelee", true, true);
            player.SetPerk("specialty_heartbreaker", true, true);
            player.SetPerk("specialty_selectivehearing", true, true);
            player.SetPerk("specialty_fastsnipe", true, true);
            player.SetPerk("specialty_improvedholdbreath", true, true);
            //player.SetField("specialty_primarydeath", true);Test??
            player.SetPerk("specialty_spygame", true, true);
            player.SetPerk("specialty_spygame2", true, true);
            player.SetPerk("specialty_automantle", true, true);
            player.SetPerk("specialty_fastsprintrecovery", true, true);
            //Hardline(player);
            player.SetPerk("specialty_jhp", true, true);//use Deathstreak?
            player.SetPerk("specialty_stalker", true, true);
            SteadyAimPro(player);
            //DoubleLoad(player);//Code conflicts methods now. Recode to OnPlayerDamage
            player.SetPerk("specialty_quickswap", true, true);
            player.SetPerk("specialty_lowprofile", true, true);
            player.SetPerk("specialty_empimmune", true, true);//This might need to be made, shows in codelist but not perklist
            player.SetPerk("specialty_throwback", true, true);
            player.SetField("specialty_assists", 1);//TODO: Figure out assist code
            player.SetPerk("specialty_fastoffhand", true, true);
            player.SetField("specialty_paint_pro", 1);
            Juiced(player);
            player.SetPerk("specialty_grenadepulldeath", true, true);
            player.SetPerk("specialty_pistoldeath", true, true);
            //player.SetPerk("specialty_bulletdamage", true, true);//This is Nightvision, determine if we want this
            CombatHigh(player);
            player.SetPerk("specialty_bulletpenetration", true, true);
            //OnInterval(100, () => Marksman(player));
            SharpFocus(player);
            player.SetPerk("specialty_holdbreathwhileads", true, true);
            player.SetPerk("specialty_longerrange", true, true);
            player.SetPerk("specialty_fastermelee", true, true);
            player.SetPerk("specialty_reducedsway", true, true);
            player.SetPerk("specialty_fastmeleerecovery", true, true);
            player.SetPerk("specialty_freerunner", true, true);
            //player.SetField("specialty_luckycharm", true);//Figure out the easiest way for this to work, higher CP chances
            player.SetPerk("specialty_exposeenemy", true, true);
            //Teamperk_StoppingPower(player);
        }
        public void LocalJammer(Entity player)
        {
            Entity Jammer = Call<Entity>("spawn", "script_model", player.Origin);
            Jammer.Call("linkto", player, "tag_origin");
            Jammer.SetField("team", player.GetField<string>("sessionteam"));
            Call(32777, Jammer, player);
            Jammer.OnInterval(500, (j) =>
                {
                    if (!player.IsAlive)
                    {
                        j.Call("delete");
                        return false;
                    }
                    else return true;
                });
        }
        public void Shield(Entity player)
        {
            AfterDelay(150, () =>
            player.Call("attachshieldmodel", "weapon_riot_shield_mp", "tag_shield_back"));
        }
        public void Thermal(Entity player)
        {
            //we dont want this to be forever annoying, so we should add a toggle
            var ThrmlTxt2 = HudElem.CreateFontString(player, "default", 1f);
            ThrmlTxt2.SetPoint("BOTTOMRIGHT", "BOTTOMRIGHT", -10, -18);
            ThrmlTxt2.HideWhenInMenu = true;
            ThrmlTxt2.SetText("Toggle Thermal");
            var ThrmlTxt = HudElem.CreateFontString(player, "default", 1.1f);
            ThrmlTxt.SetPoint("BOTTOMRIGHT", "BOTTOMRIGHT", -35, -5);
            ThrmlTxt.HideWhenInMenu = true;
            ThrmlTxt.SetText("^3[{vote yes}]");
            player.SetField("ThrmlTxt", new Parameter(ThrmlTxt));
            player.SetField("ThrmlImg", new Parameter(ThrmlTxt2));
            player.SetField("ThermalToggleSet", 1);
            player.SetField("ThermalOn", 0);
        }
        public void DestroyThermalText(Entity player, HudElem ThrmlImg, HudElem ThrmlTxt)
        {
            if (player.GetField<int>("ThermalToggleSet") != 1) return;
            ThrmlImg.Call<HudElem>("destroy");
            ThrmlTxt.Call<HudElem>("destroy");
            player.SetField("ThermalToggleSet", 0);
        }
        public void BlackBox(Entity player)
        {
            player.SetField("BlackBox", 1);
            HeliTime = 45;//Tune this. Check the actual usage code to make this right.
        }
        public void Hardline(Entity player)
        {
            player.SetField("killstreak", player.GetField<int>("killstreak") + 1);
            //CheckStreak(player);use this if we want to have a streak be aquired after this 16 streak
        }
        public void SteadyAimPro(Entity player)
        {
            player.SetField("HasSteadyAimPro", 1);
            player.Call("setaimspreadmovementscale", 0.5f);
        }
        public void DoubleLoad(Entity player)
        {
            /*
            OnNotify("reload", () =>
                {
                    if (player.GetField<int>("HasDiedWithPerks") == 1) return;
                    int ammoInClip = player.GetWeaponAmmoClip(player.CurrentWeapon);
                    int clipSize = player.Call<int>(388, player.CurrentWeapon);
                    int difference = clipSize - ammoInClip;
                    int ammoReserves = player.GetWeaponAmmoStock(player.CurrentWeapon);

                    if (ammoInClip != clipSize && ammoReserves > 0)
                    {

                        if (ammoInClip + ammoReserves >= clipSize)
                        {
                            player.Call("setWeaponAmmoClip", player.CurrentWeapon, clipSize);
                            player.Call("setWeaponAmmoStock", player.CurrentWeapon, (ammoReserves - difference));
                        }
                        else
                        {
                            player.Call("setWeaponAmmoClip", player.CurrentWeapon, ammoInClip + ammoReserves);

                            if (ammoReserves - difference > 0)
                                player.Call("setWeaponAmmoStock", player.CurrentWeapon, (ammoReserves - difference));
                            else
                                player.Call("setWeaponAmmoStock", player.CurrentWeapon, 0);
                        }
                    }
                });
             */
            //Code says this is reload after every kill... Maybe recode this in OnPlayerKilled.
        }
        public void Juiced(Entity player)
        {
            player.SetField("JuicedActive", 1);
            var juicedTimer = HudElem.CreateFontString(player, "hudsmall", 1.0f);
	        juicedTimer.SetPoint("CENTER", "CENTER", 0, 80);
	        juicedTimer.Call<HudElem>("settimer", 7.0f );
	        juicedTimer.Color = new Vector3(0.8f, 0.8f, 0);
	        juicedTimer.Archived = false;
	        juicedTimer.Foreground = true;

	        var juicedIcon = HudElem.CreateIcon(player, "specialty_juiced", 32, 32 );
	        juicedIcon.Alpha = 0;
	        juicedIcon.Parent = juicedTimer;
	        juicedIcon.SetPoint( "BOTTOM", "TOP" );
	        juicedIcon.Archived = true;
	        juicedIcon.Sort = 1;
	        juicedIcon.Foreground = true;
            juicedIcon.Call<HudElem>("fadeovertime", 2.0f);
	        juicedIcon.Alpha = 0.85f;

            player.SetField("juicedTimer", new Parameter(juicedTimer));
            player.SetField("juicedIcon", new Parameter(juicedIcon));
            OnInterval(10, () =>
                {
                    player.Call("setmovespeedscale", 1.25f);
                    if (player.GetField<int>("JuicedActive") == 1) return true;
                    else
                    {
                        player.Call("setmovespeedscale", 1);
                        return false;
                    }
                });
            AfterDelay(5000, () =>
                {
                        juicedTimer.Call<HudElem>("fadeovertime", 2.0f);
                        juicedTimer.Alpha = 0;
                        juicedIcon.Call<HudElem>("fadeovertime", 2.0f);
                        juicedIcon.Alpha = 0;
                });
            AfterDelay(7000, () =>
                {
                    player.SetField("JuicedActive", 0);
                    RemoveJuicedIcon(player, juicedTimer, juicedIcon);
                    player.Call("setmovespeedscale", 1);
                });
        }
        public void RemoveJuicedIcon(Entity player, HudElem juicedTimer, HudElem juicedIcon)
        {
            juicedTimer.Call<HudElem>("destroy");
            juicedIcon.Call<HudElem>("destroy");
            if (player.GetField<int>("juicedActive") != 0)
                player.SetField("JuicedActive", 0);
        }
        public void CombatHigh(Entity player)
        {
            player.SetField("CombatHighActive", 1);
            HudElem combatHighOverlay = HudElem.NewClientHudElem(player);
	        combatHighOverlay.X = 0;
	        combatHighOverlay.Y = 0;
	        combatHighOverlay.AlignX = "left";
	        combatHighOverlay.AlignY = "top";
	        combatHighOverlay.HorzAlign = "fullscreen";
	        combatHighOverlay.VertAlign = "fullscreen";
	        combatHighOverlay.SetShader( "combathigh_overlay", 640, 480 );
	        combatHighOverlay.Sort = -10;
	        combatHighOverlay.Archived = true;

            var combatHighTimer = HudElem.CreateFontString(player, "hudsmall", 1.0f);
            combatHighTimer.SetPoint("CENTER", "CENTER", 0, 112);
            combatHighTimer.Call<HudElem>("settimer", 10.0f);
            combatHighTimer.Color = new Vector3(0.8f, 0.8f, 0);
            combatHighTimer.Archived = false;
            combatHighTimer.Foreground = true;

            var combatHighIcon = HudElem.CreateIcon(player, "hint_health", 32, 32);
            combatHighIcon.Alpha = 0;
            combatHighIcon.Parent = combatHighTimer;
            combatHighIcon.SetPoint("BOTTOM", "TOP");
            combatHighIcon.SetShader("hint_health", 32, 32);
            combatHighIcon.Archived = true;
            combatHighIcon.Sort = 1;
            combatHighIcon.Foreground = true;

            combatHighIcon.Call<HudElem>("fadeovertime", 1.0f);
            combatHighIcon.Alpha = 0.85f;
            combatHighOverlay.Call<HudElem>("fadeovertime", 1.0f);
            combatHighOverlay.Alpha = 1.0f;	

            player.SetField("combatHighTimer", new Parameter(combatHighTimer));
            player.SetField("combatHighIcon", new Parameter(combatHighIcon));
            player.SetField("combatHighOverlay", new Parameter(combatHighOverlay));

            player.SetField("maxhealth", 500);
            player.Health = 500;

            AfterDelay(8000, () =>
            {
                    combatHighTimer.Call<HudElem>("fadeovertime", 2.0f);
                    combatHighTimer.Alpha = 0;
                    combatHighIcon.Call<HudElem>("fadeovertime", 2.0f);
                    combatHighIcon.Alpha = 0;
                    combatHighOverlay.Call<HudElem>("fadeovertime", 2.0f);
                    combatHighOverlay.Alpha = 0;
            });
            AfterDelay(10000, () =>
            {
                player.SetField("CombatHighActive", 0);
                player.SetField("maxhealth", 100);
                player.Health = 100;
                RemoveCombatHighIcon(player, combatHighTimer, combatHighIcon, combatHighOverlay);
            });
        }
        public void RemoveCombatHighIcon(Entity player, HudElem combatHighTimer, HudElem combatHighIcon, HudElem combatHighOverlay)
        {
            combatHighTimer.Call<HudElem>("destroy");
            combatHighIcon.Call<HudElem>("destroy");
            combatHighOverlay.Call<HudElem>("destroy");
            if (player.GetField<int>("CombatHighActive") != 0)
                player.SetField("CombatHighActive",0);
        }
        public bool Marksman(Entity player)
        {
            if (player.GetField<int>("HasDiedWithPerks") == 1)
                return false;
            player.Call("recoilscaleoff", 0);//Maybe not this? Try off if not this. If not that, then we need to find a good value for this scalar
            return true;
        }
        public void SharpFocus(Entity player)
        {
            if (player.GetField<int>("HasSharpFocus") == 1) return;
            player.SetField("HasSharpFocus", 1);
            player.Call("setviewkickscale", .50f);
        }
        public void Teamperk_StoppingPower(Entity self)
        {
            self.OnInterval(50, (s) =>
                {
                    foreach (Entity player in Players)
                    {
                        float distance = Call<float>("distancesquared", player.Origin, self.Origin);
                        if (distance < 262144 && self != player && player.GetField<int>("HasTeamPerk") == 0 && player.GetField<string>("sessionteam") == self.GetField<string>("sessionteam"))
                        {
                            player.SetPerk("specialty_jhp", true, true);//Maybe not this. If we have no access to a damage perk, then this must be removed
                            player.SetField("HasTeamPerk", 1);
                            self.Call("iprintlnbold", "Teamperk Shared.");//test proximity range
                            player.Call("iprintlnbold", "You now have a Teamperk.");//test proximity range
                        }
                        else if (distance >= 262144 && self != player && player.GetField<int>("HasTeamPerk") == 1 && player.GetField<string>("sessionteam") == self.GetField<string>("sessionteam"))
                        {
                            player.Call("unsetperk", "jhp");
                            player.SetField("HasTeamPerk", 0);
                            self.Call("iprintlnbold", "Teamperk Removed.");//test proximity range
                            player.Call("iprintlnbold", "You lost your Teamperk.");//test proximity range
                        }
                        //Possibly add a HUD to this to notify the player they have the Teamperk from self's proximity
                    }
                    if (self.GetField<int>("HasDiedWithPerks") == 1) return false;
                    else return true;
                });
        }

        public void SetKSBoxes(Entity player)
        {
            HudElem[] ksBox = player.GetField<HudElem[]>("ksBoxes");
            int nextStreak = GetNextStreakInList(player);
            for (int i = 0; i == nextStreak; i++)
            {
                ksBox[i].SetShader("hud_killstreak_bar_empty", 10, 15);
                if (StreakBarThink(player, i)) ksBox[i].SetShader("hud_killstreak_bar_full", 10, 15);
            }
        }

        public void SetKillstreakCounter(Entity player)
        {
            player.Call("SetPlayerData", "killstreaksState", "nextIndex", 0);
            player.Call("SetPlayerData", "killstreaksState", "numAvailable", 3);
            player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("predator_missile"));
            player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
            player.Call("SetPlayerData", "killstreaksState", "icons", 1, getKillstreakIndex("emp"));
            player.Call("SetPlayerData", "killstreaksState", "hasStreak", 1, false);
            player.Call("SetPlayerData", "killstreaksState", "icons", 2, getKillstreakIndex("nuke"));
            player.Call("SetPlayerData", "killstreaksState", "hasStreak", 2, false);
            player.Call("SetPlayerData", "killstreaksState", "icons", 3, getKillstreakIndex("remote_tank"));
            player.Call("SetPlayerData", "killstreaksState", "hasStreak", 3, false);
            player.Call("SetPlayerData", "killstreaksState", "hasStreak", 4, false);
            OnInterval(50, () =>
                {
                    int curStreak = player.GetField<int>("killstreak");
                    player.Call("SetPlayerData", "killstreaksState", "count", curStreak);
                    player.Call("SetPlayerData", "killstreaksState", "countToNext", GetNextStreakInList(player));
                    if (!player.IsAlive) return false;
                    else return true;
                });
            if (player.GetField<List<string>>("StreakList").Contains("uav_strike"))
            {
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("giveWeapon", "uav_strike_marker_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "uav_strike_marker_mp");
            }
            if (player.GetField<List<string>>("StreakList").Contains("team_ammo_refill"))
            {
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 1, true);
                player.Call("giveWeapon", "killstreak_uav_mp", 0, false);
                player.Call("setActionSlot", 5, "weapon", "killstreak_uav_mp");
            }
            if (player.GetField<List<string>>("StreakList").Contains("gl_turret"))
            {
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 2, true);
                player.Call("giveWeapon", "killstreak_emp_mp", 0, false);
                player.Call("setActionSlot", 6, "weapon", "killstreak_emp_mp");
            }
            if (player.GetField<List<string>>("StreakList").Contains("sentry_gl"))
            {
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("giveWeapon", "killstreak_sentry_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_sentry_mp");
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("sentry"));
            }
            if (player.GetField<List<string>>("StreakList").Contains("mobile_mortar"))
            {
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 3, true);
                player.Call("giveWeapon", "killstreak_helicopter_mp", 0, false);
                player.Call("setActionSlot", 7, "weapon", "killstreak_helicopter_mp");
                //player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("sentry"));
            }
        }

        private bool StreakBarThink(Entity player, int index)
        {
            int KS = player.GetField<int>("killstreak");
            if (KS >= index) return true;
            else return false;
        }

        private int GetNextStreakInList(Entity player)
        {
            int killstreak = player.GetField<int>("killstreak");
            if (killstreak < 5) return 5;
            else if (killstreak == 5) return 6;
            else if (killstreak == 6 || killstreak == 7) return 8;
            else if (killstreak > 8 && killstreak < 14) return 14;
            else if (killstreak >= 14) return 16;
            else return 16;
        }

        private void SetNextStreakInList(Entity player)
        {
            List<string> StreakList = player.GetField<List<string>>("StreakList");
            if (StreakList.Count == 0)
            {
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                player.SetField("customStreak", string.Empty);
                return;
            }
            switch (StreakList[0])
            {
                case "uav_strike":
                player.SetField("customStreak", "uav_strike");
                player.Call("giveWeapon", "uav_strike_marker_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "uav_strike_marker_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("predator_missile"));
                    break;
                case "team_ammo_refill":
                player.SetField("customStreak", "team_ammo_refill");
                player.Call("giveWeapon", "killstreak_uav_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_uav_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("emp"));
                    break;
                case "mobile_mortar":
                player.SetField("customStreak", "mobile_mortar");
                player.Call("giveWeapon", "killstreak_helicopter_mp", 0, false);
                player.Call("setActionSlot", 7, "weapon", "killstreak_helicopter_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 3, true);
                //player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("remote_tank"));
                    break;
                case "gl_turret":
                player.SetField("customStreak", "gl_turret");
                player.Call("giveWeapon", "killstreak_emp_mp", 0, false);
                player.Call("setActionSlot", 4, "weapon", "killstreak_emp_mp");
                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("nuke"));
                    break;
                case "sentry_gl":
                    //player.SetField("customStreak", "gl_tur");
                    player.Call("giveWeapon", "killstreak_sentry_mp", 0, false);
                    player.Call("setActionSlot", 4, "weapon", "killstreak_sentry_mp");
                    player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
                    player.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("sentry"));
                    break;
            }
        }

        private static bool RemoveMortar(string s)
        {
            if (s == "mobile_mortar")
                return true;
            else return false;
        }
        private static bool RemoveUAV(string s)
        {
            if (s == "uav_strike")
                return true;
            else return false;
        }
        private static bool RemoveGL(string s)
        {
            if (s == "gl_turret")
                return true;
            else return false;
        }
        private static bool RemoveSentry(string s)
        {
            if (s == "sentry_gl")
                return true;
            else return false;
        }
            private static bool RemoveAmmo(string s)
        {
            if (s == "team_ammo_refill")
                return true;
            else return false;
        }

        private void KillstreakUseWaiter(Entity ent, string weapon)
        {
            List<string> StreakList = ent.GetField<List<string>>("StreakList");
            if (weapon == "uav_strike_marker_mp")//UAV Strike
            {
                var elem = HudElem.CreateFontString(ent, "hudlarge", 2.5f);
                elem.SetPoint("BOTTOMCENTER", "BOTTOMCENTER", 0, -60);
                elem.SetText("Lase target for Predator Strike.");
                ent.AfterDelay(3500, player => elem.Call("destroy"));
            }
            if (weapon == "killstreak_emp_mp")//CUSTOM Grenade Launcher
            {
                ent.GiveWeapon("gl_mp");
                AfterDelay(750, () => ent.TakeWeapon("killstreak_emp_mp"));
                AfterDelay(800, () =>
                            ent.SwitchToWeapon("gl_mp"));
                ent.Call("setweaponammostock", "gl_mp", 3);
                StreakList.RemoveAll(RemoveGL);
                //SetNextStreakInList(ent);
                ent.Call("SetPlayerData", "killstreaksState", "hasStreak", 2, false);
                ent.SwitchToWeapon("gl_mp");
                AfterDelay(1000, () =>
                    {
                        ent.Call("setperk", "specialty_explosivedamage", true, true);
                        ent.Call("setperk", "specialty_dangerclose", true, true);
                        ent.Call("setperk", "specialty_fastreload", true, true);
                    });
                //OnInterval(100, () =>
                //GLCheck(ent));
            }
            //if (weapon == "uav_strike_projectile_mp")//Valkyarie/AAMissile
            //{
            //    var elem = HudElem.CreateFontString(ent, "hudlarge", 2.5f);
            //    elem.SetPoint("TOPCENTER", "TOPCENTER");
            //    elem.SetText("Valkyrie Strike. Shoot and control the missiles.");
            //    ent.Call("setweaponammostock", "uav_strike_projectile_mp", 1);
            //    ent.AfterDelay(5000, player => elem.SetText(""));
            //    ent.Call("notifyonplayercommand", "valkaim", "+attack");
            //    Valkyrie(ent);
            //}
            if (weapon == "killstreak_uav_mp")//Team Ammo Refill
            {
                ent.AfterDelay(750, (entity) =>
                    {
                        ent.TakeWeapon("killstreak_uav_mp");
                        AfterDelay(50, () =>
                            ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                    });
                StreakList.RemoveAll(RemoveAmmo);
                //SetNextStreakInList(ent);
                ent.Call("SetPlayerData", "killstreaksState", "hasStreak", 1, false);
                string team = ent.GetField<string>("sessionteam");
                if (Call<string>("getdvar", "g_gametype") == "dm")
                {
                    ent.AfterDelay(1000, (player) =>
                        {
                            KSSplash(player, player, "used_team_ammo_refill");
                            Ammo(player);
                            player.Call("playlocalsound", "ammo_crate_use");
                        });
                }
                else
                {
                    AfterDelay(1000, () =>
                        {
                            foreach (Entity entity in Players)
                            {
                                if (entity.GetField<string>("sessionteam") == team)
                                {
                                    KSSplash(ent, entity, "used_team_ammo_refill");
                                    Ammo(entity);
                                    entity.Call("playlocalsound", "ammo_crate_use");
                                }
                                if (entity.GetField<string>("sessionteam") != team)
                                {
                                    //IW has the other team do no splash. Might bring this back for convenience.
                                }
                            }
                        });
                }
            }
            if (weapon == "killstreak_precision_airstrike_mp")//Super Airstrike, test the Laptop?
            {
                AfterDelay(600, () =>
                {
                    ent.Call("playlocalsound", "US_KS_hqr_airstrike");
                    ent.Call("beginlocationselection", "map_artillery_selector", true, 500);
                    OnInterval(500, () =>
                    {
                        if (ent.CurrentWeapon != "killstreak_precision_airstrike_mp")
                        {
                            ent.Call("endlocationselection");
                            return false;
                        }
                        else if (!ent.HasWeapon("killstreak_precision_airstrike_mp")) return false;
                        else return true;
                    });
                    ent.OnNotify("confirm_location", (Caller, location, direction) =>//Figure the outcome out for location selection
                    {
                        //ent.Call("setblurforplayer", 0, 0.3f);//Possibly already called in the code, so might be depricated
                        ent.Call("playlocalsound", "US_KS_ast_inbound");
                        AfterDelay(750, () =>
                                   ent.TakeWeapon("killstreak_precision_airstrike_mp"));
                        ent.Call("endlocationselection");
                        AfterDelay(50, () =>
                            ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                        ent.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                        ent.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                        string locS = (string)location;
                        string dir = (string)direction;
                        locS = locS.Split('(')[1];
                        int X;
                        int Y;
                        int directionYaw;
                        Int32.TryParse(locS.Split(',')[0], out X);
                        Int32.TryParse(locS.Split(',')[1], out Y);
                        Int32.TryParse(dir, out directionYaw);
                        Vector3 location2 = new Vector3(X, Y, 807.2477f);
                        //Convert this parameter to a Vector3 and input it. Thanks C#! you crapper...
                        SpawnSAJet(ent, location2, directionYaw);
                    });
                    ent.OnNotify("cancel_location", entity =>
                    {
                        ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon"));
                        ent.Call("endlocationselection");
                    });
                });
            }
            if (weapon == "killstreak_remote_tank_laptop_mp")//A-10
            {
                AfterDelay(600, () =>
                    {
                        ent.Call("playlocalsound", "US_KS_hqr_airstrike");
                        ent.Call("beginlocationselection", "map_artillery_selector", true, 500);
                        OnInterval(500, () =>
                        {
                            if (ent.CurrentWeapon != "killstreak_remote_tank_laptop_mp")
                            {
                                ent.Call("endlocationselection");
                                return false;
                            }
                            else if (!ent.HasWeapon("killstreak_remote_tank_laptop_mp")) return false;
                            else return true;
                        });
                        ent.OnNotify("confirm_location", (Caller, location, direction) =>//Figure the outcome out for location selection
                        {
                            //ent.Call("setblurforplayer", 0, 0.3f);//Possibly already called in the code, so might be depricated
                            ent.Call("playlocalsound", "US_KS_ast_inbound");
                            AfterDelay(750, () =>
                                       ent.TakeWeapon("killstreak_remote_tank_laptop_mp"));
                            ent.Call("endlocationselection");
                            AfterDelay(50, () =>
                                ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                            ent.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                            ent.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                            string locS = (string)location;
                            string dir = (string)direction;
                            locS = locS.Split('(')[1];
                            int X;
                            int Y;
                            int directionYaw;
                            Int32.TryParse(locS.Split(',')[0], out X);
                            Int32.TryParse(locS.Split(',')[1], out Y);
                            Int32.TryParse(dir, out directionYaw);
                            Vector3 location2 = new Vector3(X, Y, 807.2477f);
                            //Convert this parameter to a Vector3 and input it. Thanks C#! you crapper...
                            SpawnA10(ent, location2, directionYaw);
                        });
                        ent.OnNotify("cancel_location", entity =>
                        {
                            ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon"));
                            ent.Call("endlocationselection");
                        });
                    });
            }
            if (weapon == "airdrop_juggernaut_mp")//Juggernaut GL
            {
                ent.OnNotify("weapon_fired", (entity, weaponName) =>
                {
                    if ((string)weaponName != "airdrop_juggernaut_mp")
                        return;

                    AfterDelay(50, () =>
                        ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                    entity.AfterDelay(200, player => entity.TakeWeapon("airdrop_juggernaut_mp"));

                    entity.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                    entity.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                    entity.SetField("customStreak", string.Empty);

                    Vector3 playerForward = ent.Call<Vector3>("gettagorigin", "tag_weapon") + Call<Vector3>("AnglesToForward", ent.Call<Vector3>("getplayerangles")) * 100000;

                    Entity refobject = Call<Entity>("spawn", "script_model", ent.Call<Vector3>("gettagorigin", "tag_weapon_left"));
                    refobject.Call("setmodel", "tag_origin");
                    refobject.SetField("angles", ent.Call<Vector3>("getplayerangles"));
                    refobject.Call("moveto", playerForward, 100);
                    refobject.Call("hide");

                    //Announce we have juggernaut
                    string team = entity.GetField<string>("sessionteam");
                    foreach (Entity player in Players)
                    {
                        if (player.GetField<string>("sessionteam") == team)
                        {
                        	player.Call("playlocalsound", "US_1mc_use_juggernaut_02");//TODO: get local sound name
                        }
                        if (player.GetField<string>("sessionteam") != team)
                        {
                            player.Call("playlocalsound", "RU_1mc_enemy_juggernaut_01");
                        }
                    }

                    refobject.OnInterval(10, (refent) =>
                    {
                        if (CollidingSoon(refent, ent))
                        {
                            refobject.Call("moveto", refobject.Origin, 0.1f);
                            JuggDeliver(ent, refent.Origin, "GL");

                            Call("playfx", Crate_FX, refent.Origin, new Vector3(0, 0, -1));
                            //AfterDelay(10000, () => { redfx.Call("delete"); });
                            return false;
                        }

                        return true;
                    });
                });
            }
            if (weapon == "airdrop_sentry_marker_mp")//Juggernaut Recon
            {
                ent.OnNotify("weapon_fired", (entity, weaponName) =>
                {
                    if ((string)weaponName != "airdrop_sentry_marker_mp")
                        return;

                    AfterDelay(50, () =>
                        ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                    entity.AfterDelay(300, player => entity.TakeWeapon("airdrop_sentry_marker_mp"));

                    entity.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                    entity.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                    entity.SetField("customStreak", string.Empty);

                    Vector3 playerForward = ent.Call<Vector3>("gettagorigin", "tag_weapon") + Call<Vector3>("AnglesToForward", ent.Call<Vector3>("getplayerangles")) * 100000;

                    Entity refobject = Call<Entity>("spawn", "script_model", ent.Call<Vector3>("gettagorigin", "tag_weapon_left"));
                    refobject.Call("setmodel", "tag_origin");
                    refobject.SetField("angles", ent.Call<Vector3>("getplayerangles"));
                    refobject.Call("moveto", playerForward, 100);
                    refobject.Call("hide");

                    //Announce we have juggernaut
                    string team = entity.GetField<string>("sessionteam");
                    foreach (Entity player in Players)
                    {
                        if (player.GetField<string>("sessionteam") == team)
                        {
                            player.Call("playlocalsound", "US_1mc_use_juggernaut_02");//TODO: get "friendly has Juggernaut sound name!"
                        }
                        if (player.GetField<string>("sessionteam") != team)
                        {
                            player.Call("playlocalsound", "RU_1mc_enemy_juggernaut_01");//TODO: get "enemy has Juggernaut sound name!"
                        }
                    }

                    refobject.OnInterval(10, (refent) =>
                    {
                        if (CollidingSoon(refent, ent))
                        {
                            refobject.Call("moveto", refobject.Origin, 0.1f);
                            JuggDeliver(ent, refent.Origin, "Recon");

                            Call("playfx", Crate_FX, refent.Origin, new Vector3(0, 0, -1));
                            //AfterDelay(10000, () => { redfx.Call("delete"); });
                            return false;
                        }

                        return true;
                    });
                });
            }
            if (weapon == "killstreak_helicopter_mp")//Mobile Mortars
            {
                ent.AfterDelay(750, (entity) =>
                {
                    ent.TakeWeapon("killstreak_helicopter_mp");
                    AfterDelay(50, () =>
                        ent.SwitchToWeaponImmediate(ent.GetField<string>("lastDroppableWeapon")));
                });
                StreakList.RemoveAll(RemoveMortar);
                //SetNextStreakInList(ent);
                ent.Call("SetPlayerData", "killstreaksState", "hasStreak", 3, false);
                //foreach (Entity entity in Players)
                //{
                    //KSSplash(ent, entity, "");
                //}
                if (Call<string>("getdvar", "mapname") == "mp_dome")
                {
                    Call("magicbullet", "javelin_mp", new Vector3(960.279f, -482.564f, -388.872f + 8000), new Vector3(960.279f, -482.564f, -388.872f), ent);
                    Call("magicbullet", "javelin_mp", new Vector3(-921.941f, 166.449f, -418.131f + 8000), new Vector3(-921.941f, 166.449f, -418.131f), ent);
                    Call("magicbullet", "javelin_mp", new Vector3(43.3564f, 2102.85f, -290.875f + 8000), new Vector3(43.3564f, 2102.85f, -290.875f), ent);
                    //TODO: Add Tank Spawn and see if FX will be possible to add. Also replace points if they are outdated. Can also use a Mortar update, we'd need a Javelin-like Missile/Projectile. Javelin is what the code uses. 
                }
                else
                {
                    Call("iprintlnbold", "Mobile Mortar not supported in this level! Contact Slvr99.");
                }
            }
            if (weapon == "killstreak_helicopter_flares_mp")//Air Superiority
            {
                AfterDelay(50, () =>
                    ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                ent.AfterDelay(750, (entity) =>
                    ent.TakeWeapon("killstreak_helicopter_flares_mp"));
                string team = ent.GetField<string>("sessionteam");
                foreach (Entity players in Players)
                {
                    KSSplash(ent, players, "Air Superiority");
                }
                SpawnAirSup(ent);
            }
            if (weapon == "airdrop_marker_mp")//Emergency Airdrop!
            {
                //REMEBER TO ADD THE ks idle sounds on get. Ready for Delivery. Put in KS code above.
                ent.OnNotify("weapon_fired", (entity, weaponName) =>
                {
                    if ((string)weaponName != "airdrop_marker_mp")
                        return;

                    AfterDelay(50, () =>
                        ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                    entity.AfterDelay(300, player => entity.TakeWeapon("airdrop_marker_mp"));

                    entity.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                    entity.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                    entity.SetField("customStreak", string.Empty);

                    Vector3 playerForward = ent.Call<Vector3>("gettagorigin", "tag_weapon") + Call<Vector3>("AnglesToForward", ent.Call<Vector3>("getplayerangles")) * 100000;

                    Entity refobject = Call<Entity>("spawn", "script_model", ent.Call<Vector3>("gettagorigin", "tag_weapon_left"));
                    refobject.Call("setmodel", "tag_origin");
                    refobject.SetField("angles", ent.Call<Vector3>("getplayerangles"));
                    refobject.Call("moveto", playerForward, 100);
                    refobject.Call("hide"); //for some reason we have to keep the model, oh well, we'll just hide it.

                    refobject.OnInterval(10, (refent) =>
                    {
                        if (CollidingSoon(refent, ent))
                        {
                            refobject.Call("moveto", refobject.Origin, 0.1f);
                            CallEmergAirdrop(ent, refent);

                            Entity redfx = Call<Entity>("spawnfx", Crate_FX, refent.Origin);
                            Call("playfx", Crate_FX, refent.Origin, new Vector3(0, 0, -1));
                            Call("playfx", redfx, refent.Origin, new Vector3(0, 0, -1));
                            AfterDelay(10000, () => { redfx.Call("delete"); });
                            return false;
                        }

                        return true;
                    });
                });
            }
            if (weapon == "killstreak_predator_missile_mp")//Harriers
            {
                AfterDelay(600, () =>
                {
                    ent.Call("playlocalsound", "US_KS_hqr_airstrike");
                    ent.Call("beginlocationselection", "map_artillery_selector", false, 500);//Find out size properly here. Default is Dome
                    OnInterval(500, () =>
                        {
                            if (ent.CurrentWeapon != "killstreak_predator_missile_mp")
                            {
                                ent.Call("endlocationselection");
                                return false;
                            }
                            else if (!ent.HasWeapon("killstreak_predator_missile")) return false;
                            else return true;
                        });
                    ent.OnNotify("confirm_location", (Caller, location, direction) =>//Figure the outcome out for location selection
                    {
                        //ent.Call("setblurforplayer", 0, 0.3f);//Possibly already called in the code, so might be depricated
                        //ent.Call("playlocalsound", "US_KS_ast_inbound");
                        AfterDelay(750, () =>
                                   ent.TakeWeapon("killstreak_predator_missile_mp"));
                        ent.Call("endlocationselection");
                        AfterDelay(50, () =>
                            ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                        ent.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                        ent.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                        string locS = (string)location;
                        locS = locS.Split('(')[1];
                        int X;
                        int Y;
                        Int32.TryParse(locS.Split(',')[0], out X);
                        Int32.TryParse(locS.Split(',')[1], out Y);
                        Vector3 location2 = new Vector3(X, Y, 807.2477f);
                        //Convert this parameter to a Vector3 and input it. Thanks C#! you crapper...
                        SpawnHarrierJet(ent, location2);
                    });
                    ent.OnNotify("cancel_location", entity =>
                        {
                            ent.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon"));
                            ent.Call("endlocationselection");
                        });
                });
            } 
            if (weapon == "killstreak_sentry_mp")//Sentry GL
            {
                //if (ent.GetField<int>("isCarryingSentry") == 0)
                {
                    //StreakList.RemoveAll(RemoveSentry);
                    SpawnGLTurret(ent);
                    //ent.Call("notifyonplayercommand", "place_sentry", "+attack");
                    //ent.Call("notifyonplayercommand", "cancel_sentry", "+actionslot 4");
                    ent.Call(33501);
                }
            }
        }
        public void SpawnGLTurret(Entity player)
        {
            float playerGround = Call<Vector3>(120, player.Origin).Z;
            float playerAngleY = player.Call<Vector3>("getplayerangles").Y;
            Entity sentry = Call<Entity>("spawnturret", "misc_turret", new Vector3(player.Origin.X, player.Origin.Y, playerGround), "manned_gl_turret_mp");
            sentry.SetField("angles", new Vector3(0, playerAngleY, 0));
            sentry.Call("setmodel", "sentry_grenade_launcher");
            sentry.Health = 1000;
            sentry.Call(33417, true);
            sentry.Call(33418, true);
            sentry.Call(33054);
            //sentry.Call(33083, 80);
            //sentry.Call(33084, 80);
            //sentry.Call(33086, 50);
            sentry.Call(32942);
            sentry.Call(33054);
            sentry.Call(33088, -89.0f);
            sentry.Call(33122, true);
            sentry.Call(32864, "sentry");
            sentry.SetField("owner", player);
            sentry.SetField("team", player.GetField<string>("sessionteam"));
            sentry.Call(33051, "allies");
            sentry.Call(33006, player);
            sentry.Call(32982, player);//SetTurretOwner?
            sentry.SetField("isAlive", 1);
            sentry.SetField("timeLeft", 90);
            sentry.SetField("isBeingCarried", 1);
            sentry.SetField("engaging", 0);
            sentry.Call("setmodel", "sentry_grenade_launcher_obj");
            player.SetField("isCarryingSentry", 1);
            sentry.Call(33011);
            sentry.SetField("canBePlaced", 1);
            sentry.Call(32864, "sentry_offline");
            sentry.Call(33007, player);
            player.TakeWeapon("killstreak_sentry_mp");
            //sentry.Call("linkto", player, "tag_origin");
            /*
            OnInterval(10, () =>
                {
                    sentry.Call("moveto", player.Origin + Call<Vector3>("anglestoforward", new Vector3(0, player.Call<Vector3>("getplayerangles").Y, 0)) * 50, 0.1f);
                    sentry.Call("rotateto", new Vector3(0, player.Call<Vector3>("getplayerangles").Y, 0), .1f);
                    if (player.GetField<int>("isCarryingSentry") == 1) return true;
                    else return false;
                });
            player.OnNotify("place_sentry", (ent) =>
                {
                    if (sentry.GetField<int>("canBePlaced") == 1)
                    {
                        player.Call(33502);
                        player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                        player.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                        AfterDelay(750, () => player.TakeWeapon("killstreak_sentry_mp"));
                        player.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon"));
                        sentry.Call("setsentrycarrier", "");
                        PlaceGLTurret(player, sentry.Origin, sentry.GetField<Vector3>("angles"));
                        AfterDelay(700, () => player.SetField("isCarryingSentry", 0));
                        sentry.Call("delete");
                    }
                });
            player.OnNotify("cancel_sentry", (ent) =>
            {
                if (player.GetField<int>("isCarryingSentry") == 1)
                {
                    player.Call(33502);
                    player.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon"));
                    AfterDelay(700, () => player.SetField("isCarryingSentry", 0));
                    sentry.Call("delete");
                }
            });
             */
            sentry.OnInterval(50, (turret) =>
            {
                Entity carrier = turret.GetField<Entity>("owner");
                if (carrier.IsAlive && turret.GetField<int>("canBePlaced") == 1 && carrier.GetField<int>("isCarryingSentry") == 1 && carrier.Call<int>(33534) == 1)
                {
                    carrier.Call(33502);
                    player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                    player.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                    player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon"));
                    carrier.SetField("isCarryingSentry", 0);
                    sentry.Call(33007);
                    sentry.SetField("isBeingCarried", 0);
                    Vector3 ground = Call<Vector3>(120, player.Origin);
                    Vector3 playerAngles = player.Call<Vector3>("getplayerangles");
                    Vector3 anglesToForward = Call<Vector3>("anglestoforward", new Vector3(0, playerAngles.Y, 0));
                    PlaceGLTurret(player, sentry, new Vector3(player.Origin.X, player.Origin.Y, ground.Z) + anglesToForward * 50, new Vector3(0, playerAngles.Y, 0));
                    //sentry.Call("delete");
                    //HandlePickup(turret);
                    return false;
                    //sentry.Call("delete");
                }
                else if (!carrier.IsAlive)
                {
                    sentry.Call("delete");
                    return false;
                }
                else return true;
            });
        }
        public void PlaceGLTurret(Entity player, Entity turret, Vector3 origin, Vector3 angles)
        {
            foreach (Entity players in Players) KSSplash(player, players, "used_gl_turret");
            turret.Origin = origin;
            turret.SetField("angles", angles);
            turret.Call("setmodel", "sentry_grenade_launcher");
            turret.Call("playSound", "sentry_gun_plant");
            turret.Call(32864, "sentry");
            player.GetField<List<string>>("StreakList").RemoveAll(RemoveSentry);
            turret.Call(33008, true);
            turret.OnNotify("damage", (entity, damage, attacker, direction_vec, point, meansOfDeath, modelName, partName, tagName, iDFlags, weapon) =>
            {
                //Log.Write(LogLevel.All, "Damaged");
                if ((string)meansOfDeath == "MOD_MELEE") DestroySentry(turret);
                else
                {
                    turret.Health -= (int)damage;
                    if (turret.Health <= 0) DestroySentry(turret);
                }
            });
            SentryAI(turret);
        }
        public void SentryAI(Entity sentry)
        {
            sentry.OnInterval(1000, (turret) =>
            {
                if (turret.Health <= 0) return false;
                //if (turret.GetField<int>("isBeingCarried") == 1) return true;
                turret.SetField("timeLeft", turret.GetField<int>("timeLeft") - 1);
                if (turret.GetField<int>("timeLeft") > 0 && turret.GetField<Entity>("owner") != null && turret.GetField<Entity>("owner").IsPlayer) return true;
                else
                {
                    DestroySentry(turret);
                    return false;
                }
            });
            sentry.OnInterval(50, (turret) =>
            {
                if (turret.Health > 0)
                {
                    if (turret.GetField<int>("isBeingCarried") == 0)
                    {
                        Entity target = null;
                        foreach (Entity b in Players)
                        {
                            if (sentry.GetField<int>("engaging") == 1) break;
                            string gt = Call<string>("getdvar", "g_gametype");
                            if (gt != "dm")
                            {
                                if (b.GetField<string>("sessionteam") == turret.GetField<string>("team")) continue;
                            }
                            else if (b == turret.GetField<Entity>("owner")) continue;
                            Vector3 flashTag = turret.Call<Vector3>("gettagorigin", "tag_flash");
                            Vector3 botHead = b.Call<Vector3>("gettagorigin", "j_head");
                            int tracePass = Call<int>(116, flashTag, botHead, true, turret);
                            //float tracePass = b.Call<float>("sightconetrace", flashTag, b.GetField<Entity>("hitbox"));
                            if (tracePass != 1)
                            {
                                continue;
                            }
                            if (!b.IsAlive) continue;
                            float distanceSqr = Call<float>("distancesquared", turret.Origin, b.Origin);
                            if (distanceSqr < 999999999)
                                target = b;
                        }
                        if (target != null && turret.GetField<int>("engaging") != 1)
                        {
                            sentry_engageFire(turret, target);
                        }
                        else if (target == null && turret.GetField<int>("engaging") != 1)
                        {
                            turret.Call(33011);
                            //stopSentrySounds(turret);
                            //sentry.Call(32974);
                        }
                        return true;
                    }
                    else return true;
                }
                else return false;
            });
        }
        private void sentry_engageFire(Entity sentry, Entity target)
        {
            sentry.SetField("engaging", 1);
            sentry.Call(33009, target);
            sentry.AfterDelay(750, (s) =>
                {
                    //sentry.Call(32981);
                    Vector3 flashTag = s.Call<Vector3>("gettagorigin", "tag_flash");
                    Call("magicbullet", "gl_mp", flashTag, target.Origin + new Vector3(0, 0, 45), sentry.GetField<Entity>("owner"));
                });
            sentry.AfterDelay(3000, (s) =>
                s.SetField("engaging", 0));
        }
        public void DestroySentry(Entity sentry)
        {
            sentry.Call("setCanDamage", false);
            sentry.Call(33088, 40);
            sentry.Call(32864, "sentry_offline");
            sentry.Health = 0;
            //sentry.SetField("owner", null);
            sentry.Call("setmodel", "sentry_grenade_launcher_destroyed");
            sentry.Call("playSound", "sentry_explode");
            Entity owner = sentry.GetField<Entity>("owner");
            owner.Call("playlocalsound", "US_1mc_sentry_gone");
            Call("playFxOnTag", SentryExplodeFX, sentry, "tag_aim");
            AfterDelay(1500, () => sentry.Call("playSound", "sentry_explode_smoke"));
            Call("playFxOnTag", SentrySmokeFX, sentry, "tag_aim");
            sentry.AfterDelay(7000, (turret) =>
            {
                turret.Call("delete");
            });
        }
        public void CallEmergAirdrop(Entity ent, Entity location)
        {
            //Entity c130 = Call<Entity>("spawn", "script_model", new Parameter(ent.Origin + new Vector3(-10898.3592f, 0, 1799.9675f)));//OLD, use spawnPlane now
            Vector3 pathStart = ent.Origin + new Vector3(-10898.3592f, 0, 1799.9675f);
            Entity c130 = Call<Entity>(367, ent, "script_model", pathStart, "compass_objpoint_c130_friendly", "compass_objpoint_c130_enemy");
            c130.Call("setmodel", "vehicle_ac130_low_mp");
            float getNorthYaw = ent.Call<float>(123);
            c130.SetField("angles", new Parameter(new Vector3(0, getNorthYaw, 0)));
            EmergAirdropFly(ent, c130, location);
        }
        public void EmergAirdropFly(Entity ent, Entity c130, Entity dropLocation)
        {
            c130.Call("playloopsound", "veh_ac130_sonic_boom");
            c130.Call(33399, new Parameter(dropLocation.Origin + new Vector3(0, 0, 1799.9675f)), 5.2f); //moveto, TODO: get proper location and speed for flyin. Dome is Default. MAKE SURE to get random float for the entry point and make them communicate with the angles!!!
            Entity crate2 = Call<Entity>("spawn", "script_model", new Parameter(dropLocation.Origin + new Vector3(100, 0, 1799.9675f)));
            Entity crate3 = Call<Entity>("spawn", "script_model", new Parameter(dropLocation.Origin + new Vector3(200, 0, 1799.9675f)));
            Entity crate4 = Call<Entity>("spawn", "script_model", new Parameter(dropLocation.Origin + new Vector3(300, 0, 1799.9675f)));
            Entity crate = Call<Entity>("spawn", "script_model", new Parameter(dropLocation.Origin + new Vector3(0, 0, 1799.9675f)));
            string team = ent.GetField<string>("sessionteam");
            foreach (Entity entity in Players)
            {
                if (entity.GetField<string>("sessionteam") == team)
                {
                    crate.Call("setmodel", "com_plasticcase_friendly");
                    crate.Call("hide");
                    crate2.Call("setmodel", "com_plasticcase_friendly");
                    crate2.Call("hide");
                    crate3.Call("setmodel", "com_plasticcase_friendly");
                    crate3.Call("hide");
                    crate4.Call("setmodel", "com_plasticcase_friendly");
                    crate4.Call("hide");
                }
                if (entity.GetField<string>("sessionteam") != team)
                {
                    crate.Call("setmodel", "com_plasticcase_enemy");
                    crate.Call("hide");
                    crate2.Call("setmodel", "com_plasticcase_enemy");
                    crate2.Call("hide");
                    crate3.Call("setmodel", "com_plasticcase_enemy");
                    crate3.Call("hide");
                    crate4.Call("setmodel", "com_plasticcase_enemy");
                    crate4.Call("hide");
                }
            }
            AfterDelay(5200, () =>
                {
                    float Randomize = new Random().Next(360);
                    crate.SetField("angles", new Vector3(0, Randomize, 0));
                    crate.Call("clonebrushmodeltoscriptmodel", _airdropCollision);
                    crate.SetField("owner", ent.GetField<string>("name"));
                    crate2.SetField("angles", new Vector3(0, Randomize, 0));
                    crate2.Call("clonebrushmodeltoscriptmodel", _airdropCollision);
                    crate2.SetField("owner", ent.GetField<string>("name"));
                    crate3.SetField("angles", new Vector3(0, Randomize, 0));
                    crate3.Call("clonebrushmodeltoscriptmodel", _airdropCollision);
                    crate3.SetField("owner", ent.GetField<string>("name"));
                    crate4.SetField("angles", new Vector3(0, Randomize, 0));
                    crate4.Call("clonebrushmodeltoscriptmodel", _airdropCollision);
                    crate4.SetField("owner", ent.GetField<string>("name"));
                    int Force = Call<int>(152, 5);
                    Vector3 dropImpulse = new Vector3(Force, Force, Force);
                    crate.Call(33351, new Vector3(0, 0, 0), dropImpulse);
                    crate.Call("show");
                    AfterDelay(100, () =>
                        {
                    crate2.Call(33351, new Vector3(0, 0, 0), dropImpulse);
                    crate2.Call("show");
                        });
                    AfterDelay(250, () =>
                        {
                            crate3.Call(33351, new Vector3(0, 0, 0), dropImpulse);
                            crate3.Call("show");
                        });
                    AfterDelay(400, () =>
                        {
                            crate4.Call(33351, new Vector3(0, 0, 0), dropImpulse);
                            crate4.Call("show");
                        });
                    c130.Call(33399, new Parameter(dropLocation.Origin + new Vector3(10898.3592f, 0, 1799.9675f)), 5.2f);
                    int curObjID = 31 - _mapCount++;
                    crate.OnNotify("physics_finished", (crate1) =>
                        {
                            Call(431, curObjID, "active"); // objective_add
                            foreach (Entity entity in Players)
                            {
                            if (entity.GetField<string>("sessionteam") == team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_friendly"); // objective_icon
                            }
                            if (entity.GetField<string>("sessionteam") != team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_enemy"); // objective_icon enemy
                            }
                            }
                            Call(357, curObjID, crate);
                        });
                    crate2.OnNotify("physics_finished", (crate1) =>
                    {
                        Call(431, curObjID, "active"); // objective_add
                        foreach (Entity entity in Players)
                        {
                            if (entity.GetField<string>("sessionteam") == team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_friendly"); // objective_icon
                            }
                            if (entity.GetField<string>("sessionteam") != team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_enemy"); // objective_icon enemy
                            }
                        }
                        Call(357, curObjID, crate2);
                    });
                    crate3.OnNotify("physics_finished", (crate1) =>
                    {
                        Call(431, curObjID, "active"); // objective_add
                        foreach (Entity entity in Players)
                        {
                            if (entity.GetField<string>("sessionteam") == team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_friendly"); // objective_icon
                            }
                            if (entity.GetField<string>("sessionteam") != team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_enemy"); // objective_icon enemy
                            }
                        }
                        Call(357, curObjID, crate3);
                    });
                    crate4.OnNotify("physics_finished", (crate1) =>
                    {
                        Call(431, curObjID, "active"); // objective_add
                        foreach (Entity entity in Players)
                        {
                            if (entity.GetField<string>("sessionteam") == team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_friendly"); // objective_icon
                            }
                            if (entity.GetField<string>("sessionteam") != team)
                            {
                                Call(434, curObjID, "compass_objpoint_ammo_enemy"); // objective_icon enemy
                            }
                        }
                        Call(357, curObjID, crate4);
                    });
                    //These Notifies might need to be changed back to AfterDelay if physics_finished isn't a real Notify.
                    AfterDelay(3300, () =>
                        c130.Call("delete"));
                    WatchEmergCrates(ent, crate, crate2, crate3, crate4);
                });
        }
        public void JuggDeliver(Entity ent, Vector3 location, string JuggType)
        {
            //Entity c130 = Call<Entity>("spawn", "script_model", new Parameter(ent.Origin + new Vector3(-10898.3592f, 0, 1799.9675f)));//OLD. SpawnPlane is used now
            Vector3 pathStart = ent.Origin + new Vector3(-10898.3592f, 0, 1799.9675f);
            Entity c130 = Call<Entity>(367, ent, "script_model", pathStart, "compass_objpoint_c130_friendly", "compass_objpoint_c130_enemy");
            c130.Call("setmodel", "vehicle_ac130_low_mp");
            float getNorthYaw = ent.Call<float>(123);
            c130.SetField("angles", new Parameter(new Vector3(0, getNorthYaw, 0)));
            //int curObjID = 31 - _mapCount++;
            //Call(431, curObjID, "active"); // objective_add
            //string team = ent.GetField<string>("sessionteam");
            //foreach (Entity entity in Players)
            //{
                //if (entity.GetField<string>("sessionteam") == team)
                //{
                   // Call(434, curObjID, "compass_objpoint_c130_friendly"); // objective_icon
                //}
                //if (entity.GetField<string>("sessionteam") != team)
                //{
                    //Call(434, curObjID, "compass_objpoint_c130_enemy"); // objective_icon enemy
                //}
            //}
            //Call(435, curObjID, new Parameter(c130.Origin)); // objective_position
            if (JuggType == "GL")
                JuggFly(ent, c130, location, "GL");
            else if (JuggType == "Recon")
                JuggFly(ent, c130, location, "Recon");
            else return;
        }
        public void JuggFly(Entity ent, Entity c130, Vector3 dropLocation, string JuggType)
        {
        	c130.Call("playloopsound", "veh_ac130_sonic_boom");
            c130.Call(33399, new Parameter(dropLocation + new Vector3(0, 0, 1799.9675f)), 5.5f); //moveto, TODO: get this to fly to proper height ABOVE dropLocation.Origin
            Entity crate = Call<Entity>("spawn", "script_model", new Parameter(dropLocation + new Vector3(0, 0, 1799.9675f)));
            string team = ent.GetField<string>("sessionteam");
            float Randomize = new Random().Next(360);
            crate.SetField("angles", new Vector3(0, Randomize, 0));
            crate.SetField("owner", ent.GetField<string>("name"));
            foreach (Entity entity in Players)
            {
                if (entity.GetField<string>("sessionteam") == team)
                {
                    crate.Call("setmodel", "com_plasticcase_friendly");
                    crate.Call("hide");
                }
                if (entity.GetField<string>("sessionteam") != team)
                {
                    crate.Call("setmodel", "com_plasticcase_enemy");
                    crate.Call("hide");
                }
            }
            AfterDelay(5500, () =>
                {
                    crate.Call("show");
                    crate.Call("clonebrushmodeltoscriptmodel", _airdropCollision);
                    int Force = Call<int>(152, 5);
                    Vector3 dropImpulse = new Vector3(Force, Force, Force);
                    crate.Call(33351, new Vector3(0, 0, 0), dropImpulse);
                    c130.Call(33399, new Parameter(dropLocation + new Vector3(10898.3592f, 0, 1799.9675f)), 5.5f); //moveto, TODO: get this to fly to proper area similar to spawnarea and delete.
                    AfterDelay(3500, () =>
                        {
                            c130.Call("delete");
                        });
                });
            if (JuggType == "GL")
                WatchJuggCrate(ent, crate, "GL");
            if (JuggType == "Recon")
                WatchJuggCrate(ent, crate, "Recon");
        }
        public void WatchEmergCrates(Entity ent, Entity crate, Entity crate2, Entity crate3, Entity crate4)
        {
            HudElem message = HudElem.CreateFontString(ent, "hudbig", 0.6f);
            message.SetPoint("CENTER", "CENTER", 0, 150);

            AfterDelay(90000, () =>
                {
                    crate.Call("delete");
                    crate2.Call("delete");
                    crate3.Call("delete");
                    crate4.Call("delete");
                });

            ent.Call("notifyonplayercommand", "triggeruse", "+activate");
            int? cp = new Random().Next(11);
            int? cp2 = new Random().Next(11);
            int? cp3 = new Random().Next(11);
            int? cp4 = new Random().Next(11);
            //TODO: Fix bugzilla 10253 where this Randomizer picks the same int due to the timing. Either add a time change or group the cases differently each time.
            OnInterval(100, () =>
            {
                bool _changed = false;
                    if (ent.Origin.DistanceTo(crate.Origin) < 85)
                    {
                        switch (cp)
                        {
                            case 0://Team Ammo Refill
                                message.SetText(CarePackText(ent, "team_ammo_refill"));
                                PrimeCrate(ent, crate, "team_ammo_refill");
                                break;
                            case 1://UAV Strike
                                message.SetText(CarePackText(ent, "uav_strike"));
                                PrimeCrate(ent, crate, "uav_strike");
                                break;
                            case 2://Grenade Launcher
                                message.SetText(CarePackText(ent, "gl_mp"));
                                PrimeCrate(ent, crate, "gl_mp");
                                break;
                            case 3://Super Airstrike
                                message.SetText(CarePackText(ent, "super_airstrike"));
                                PrimeCrate(ent, crate, "super_airstrike");
                                break;
                            case 4://Jugg GL
                                message.SetText(JuggText("GL"));
                                PrimeCrate(ent, crate, "jugg_gl");
                                break;
                            case 5://Jugg Recon
                                message.SetText(JuggText("Recon"));
                                PrimeCrate(ent, crate, "jugg_recon");
                                break;
                            case 6://A10
                                message.SetText(CarePackText(ent, "a10"));
                                PrimeCrate(ent, crate, "a10");
                                break;
                            case 7://Mortars
                                message.SetText(CarePackText(ent, "mobile_mortars"));
                                PrimeCrate(ent, crate, "mobile_mortars");
                                break;
                            case 8://Air Superiority
                                message.SetText(CarePackText(ent, "aastrike"));
                                PrimeCrate(ent, crate, "aastrike");
                                break;
                            case 9://Harriers
                                message.SetText(CarePackText(ent, "harriers"));
                                PrimeCrate(ent, crate, "harriers");
                                break;
                            case 10://Ammo
                                message.SetText(CarePackText(ent, "ammo"));
                                PrimeCrate(ent, crate, "ammo");
                                break;
                                //Also can duplicate lower streaks to simulate real odds
                        }
                        _changed = true;
                    }
                    if (ent.Origin.DistanceTo(crate2.Origin) < 85)
                    {
                        switch (cp2)
                        {
                            case 5://Team Ammo Refill
                                message.SetText(CarePackText(ent, "team_ammo_refill"));
                                PrimeCrate(ent, crate2, "team_ammo_refill");
                                break;
                            case 3://UAV Strike
                                message.SetText(CarePackText(ent, "uav_strike"));
                                PrimeCrate(ent, crate2, "uav_strike");
                                break;
                            case 1://Grenade Launcher
                                message.SetText(CarePackText(ent, "gl_mp"));
                                PrimeCrate(ent, crate2, "gl_mp");
                                break;
                            case 4://Super Airstrike
                                message.SetText(CarePackText(ent, "super_airstrike"));
                                PrimeCrate(ent, crate2, "super_airstrike");
                                break;
                            case 2://Jugg GL
                                message.SetText(JuggText("GL"));
                                PrimeCrate(ent, crate2, "jugg_gl");
                                break;
                            case 10://Jugg Recon
                                message.SetText(JuggText("Recon"));
                                PrimeCrate(ent, crate2, "jugg_recon");
                                break;
                            case 9://A10
                                message.SetText(CarePackText(ent, "a10"));
                                PrimeCrate(ent, crate2, "a10");
                                break;
                            case 8://Mortars
                                message.SetText(CarePackText(ent, "mobile_mortars"));
                                PrimeCrate(ent, crate2, "mobile_mortars");
                                break;
                            case 6://Air Superiority
                                message.SetText(CarePackText(ent, "aastrike"));
                                PrimeCrate(ent, crate2, "aastrike");
                                break;
                            case 7://Harriers
                                message.SetText(CarePackText(ent, "harriers"));
                                PrimeCrate(ent, crate2, "harriers");
                                break;
                            case 0://Ammo
                                message.SetText(CarePackText(ent, "ammo"));
                                PrimeCrate(ent, crate2, "ammo");
                                break;
                            //Also can duplicate lower streaks to simulate real odds
                        }
                        _changed = true;
                    }
                    if (ent.Origin.DistanceTo(crate3.Origin) < 85)
                    {
                        switch (cp3)
                        {
                            case 1://Team Ammo Refill
                                message.SetText(CarePackText(ent, "team_ammo_refill"));
                                PrimeCrate(ent, crate3, "team_ammo_refill");
                                break;
                            case 2://UAV Strike
                                message.SetText(CarePackText(ent, "uav_strike"));
                                PrimeCrate(ent, crate3, "uav_strike");
                                break;
                            case 3://Grenade Launcher
                                message.SetText(CarePackText(ent, "gl_mp"));
                                PrimeCrate(ent, crate3, "gl_mp");
                                break;
                            case 5://Super Airstrike
                                message.SetText(CarePackText(ent, "super_airstrike"));
                                PrimeCrate(ent, crate3, "super_airstrike");
                                break;
                            case 6://Jugg GL
                                message.SetText(JuggText("GL"));
                                PrimeCrate(ent, crate3, "jugg_gl");
                                break;
                            case 7://Jugg Recon
                                message.SetText(JuggText("Recon"));
                                PrimeCrate(ent, crate3, "jugg_recon");
                                break;
                            case 10://A10
                                message.SetText(CarePackText(ent, "a10"));
                                PrimeCrate(ent, crate3, "a10");
                                break;
                            case 4://Mortars
                                message.SetText(CarePackText(ent, "mobile_mortars"));
                                PrimeCrate(ent, crate3, "mobile_mortars");
                                break;
                            case 9://Air Superiority
                                message.SetText(CarePackText(ent, "aastrike"));
                                PrimeCrate(ent, crate3, "aastrike");
                                break;
                            case 0://Harriers
                                message.SetText(CarePackText(ent, "harriers"));
                                PrimeCrate(ent, crate3, "harriers");
                                break;
                            case 8://Ammo
                                message.SetText(CarePackText(ent, "ammo"));
                                PrimeCrate(ent, crate3, "ammo");
                                break;
                            //Also can duplicate lower streaks to simulate real odds
                        }
                        _changed = true;
                    }
                    if (ent.Origin.DistanceTo(crate4.Origin) < 85)
                    {
                        switch (cp4)
                        {
                            case 2://Team Ammo Refill
                                message.SetText(CarePackText(ent, "team_ammo_refill"));
                                PrimeCrate(ent, crate4, "team_ammo_refill");
                                break;
                            case 4://UAV Strike
                                message.SetText(CarePackText(ent, "uav_strike"));
                                PrimeCrate(ent, crate4, "uav_strike");
                                break;
                            case 0://Grenade Launcher
                                message.SetText(CarePackText(ent, "gl_mp"));
                                PrimeCrate(ent, crate4, "gl_mp");
                                break;
                            case 6://Super Airstrike
                                message.SetText(CarePackText(ent, "super_airstrike"));
                                PrimeCrate(ent, crate4, "super_airstrike");
                                break;
                            case 1://Jugg GL
                                message.SetText(JuggText("GL"));
                                PrimeCrate(ent, crate4, "jugg_gl");
                                break;
                            case 8://Jugg Recon
                                message.SetText(JuggText("Recon"));
                                PrimeCrate(ent, crate4, "jugg_recon");
                                break;
                            case 7://A10
                                message.SetText(CarePackText(ent, "a10"));
                                PrimeCrate(ent, crate4, "a10");
                                break;
                            case 10://Mortars
                                message.SetText(CarePackText(ent, "mobile_mortars"));
                                PrimeCrate(ent, crate4, "mobile_mortars");
                                break;
                            case 3://Air Superiority
                                message.SetText(CarePackText(ent, "aastrike"));
                                PrimeCrate(ent, crate4, "aastrike");
                                break;
                            case 5://Harriers
                                message.SetText(CarePackText(ent, "harriers"));
                                PrimeCrate(ent, crate4, "harriers");
                                break;
                            case 9://Ammo
                                message.SetText(CarePackText(ent, "ammo"));
                                PrimeCrate(ent, crate4, "ammo");
                                break;
                            //Also can duplicate lower streaks to simulate real odds
                        }
                        _changed = true;
                }
                if (!_changed)
                {
                    message.SetText("");
                }
                return true;
                //TODO: If PrimeCrate stacks up when text is present and another Crate is selected, then use the seperate threads (PrimeCrate2, 3, 4()) to avoid this.
            });
        }
        public void PrimeCrate(Entity player, Entity crate, string Killstreak)
        {
                OnNotify("triggeruse", () =>
                {
                    if (Killstreak == "team_ammo_refill" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");//If this is exploitable/unstable, we'll manually add Killstreak code instead of CheckStreak()
                                    player.SetField("killstreak", 6);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "team_ammo_refill" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", 6);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "uav_strike" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", 4);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "uav_strike" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", 4);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "gl_mp" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", -1);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "gl_mp" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", -1);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "super_airstrike" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", 9);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "super_airstrike" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", 9);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "a10" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", 11);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "a10" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                                {
                                    player.Call("freezeControls", false);
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    crate.Call("delete");
                                    int Streak = player.GetField<int>("killstreak");
                                    player.SetField("killstreak", 11);
                                    CheckStreak(player);
                                    player.SetField("killstreak", Streak);
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                                {
                                    AfterDelay(50, () =>
                                            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                    player.Call("freezeControls", false);
                                }
                            });
                    }
                    if (Killstreak == "mobile_mortars" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 14);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "mobile_mortars" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 14);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "aastrike" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 12);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "aastrike" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 12);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "harriers" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 7);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "harriers" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 7);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "jugg_gl" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                {
                                    player.Call("freezeControls", false);
                                    crate.Call("delete");
                                    player.TakeAllWeapons();
                                    player.GiveWeapon("gl_mp");
                                    player.Call("setmovespeedscale", new Parameter((float)0.65));
                                    AfterDelay(100, () =>
                                        player.SwitchToWeaponImmediate("gl_mp"));
                                    AfterDelay(200, () =>//Failsafe for swap bug
                                        player.SwitchToWeaponImmediate("gl_mp"));
                                    OnInterval(50, () =>
                                        {
                                            if (player.IsAlive)
                                                player.Call("setweaponammostock", "gl_mp", 2);
                                            return true;
                                        });
                                    player.SetPerk("specialty_radarjuggernaut", true, true);
                                    player.SetPerk("specialty_blindeye", true, true);
                                    player.SetPerk("_specialty_blastshield", true, true);
                                    player.SetPerk("specialty_bulletaccuracy", true, true);
                                    player.SetField("maxhealth", 1250);
                                    player.Health = 1250;
                                    HudElem JuggOverlay = HudElem.CreateIcon(player, "juggernaut_overlay_alpha_col", 1920, 1080);
                                    JuggOverlay.SetPoint("CENTER", "CENTER", 0, 0);
                                    JuggOverlay.Alpha = 1;
                                    OnInterval(100, () =>
                                        {
                                            if (!player.IsAlive)
                                            {
                                                JuggOverlay.Alpha = 0;
                                                return true;
                                            }
                                            else return true;
                                        });
                                    //TODO: add Jugg Model switch!
                                    foreach (Entity players in Players)
                                    {
                                        /*
                                        var splash = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                        splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 80);
                                        var splashname = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                        splashname.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 100);
                                        string team = player.GetField<string>("sessionteam");
                                        foreach (Entity entity in Players)
                                        {
                                            if (entity.GetField<string>("sessionteam") == team)
                                            {
                                                splash.SetText("Juggernaut");
                                                AfterDelay(3000, () => splash.SetText(""));
                                                splashname.SetText("^2" + player.GetField<string>("name"));
                                                AfterDelay(3000, () => splashname.SetText(""));
                                                entity.Call("playlocalsound", "mp_cardslide_v6");
                                                //TODO: fix Slide-in sound
                                            }
                                            if (entity.GetField<string>("sessionteam") != team)
                                            {
                                                splash.SetText("Juggernaut");
                                                AfterDelay(3000, () => splash.SetText(""));
                                                splashname.SetText("^1" + player.GetField<string>("name"));
                                                AfterDelay(3000, () => splashname.SetText(""));
                                                entity.Call("playlocalsound", "mp_cardslide_v6");
                                                //TODO: fix Slide-in sound
                                            }
                                        }
                                         */
                                        KSSplash(player, players, "Juggernaut");
                                    }
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                {
                                    AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                }
                            });
                    }
                    if (Killstreak == "jugg_gl" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                            {
                                if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive && crate.GetField<string>("owner") != player.GetField<string>("name"))
                                {
                                    player.Call("freezeControls", false);
                                    crate.Call("delete");
                                    player.TakeAllWeapons();
                                    player.GiveWeapon("gl_mp");
                                    player.Call("setmovespeedscale", new Parameter((float)0.65));
                                    AfterDelay(100, () =>
                                        player.SwitchToWeaponImmediate("gl_mp"));
                                    AfterDelay(200, () =>//Failsafe for swap bug
                                        player.SwitchToWeaponImmediate("gl_mp"));
                                    OnInterval(50, () =>
                                        {
                                            if (player.IsAlive)
                                                player.Call("setweaponammostock", "gl_mp", 2);
                                            return true;
                                        });
                                    player.SetPerk("specialty_radarjuggernaut", true, true);
                                    player.SetPerk("specialty_blindeye", true, true);
                                    player.SetPerk("_specialty_blastshield", true, true);
                                    player.SetPerk("specialty_bulletaccuracy", true, true);
                                    player.SetField("maxhealth", 1250);
                                    player.Health = 1250;
                                    HudElem JuggOverlay = HudElem.CreateIcon(player, "juggernaut_overlay_alpha_col", 1920, 1080);
                                    JuggOverlay.SetPoint("CENTER", "CENTER", 0, 0);
                                    JuggOverlay.Alpha = 1;
                                    OnInterval(100, () =>
                                        {
                                            if (!player.IsAlive)
                                            {
                                                JuggOverlay.Alpha = 0;
                                                return true;
                                            }
                                            else return true;
                                        });
                                    //TODO: add Jugg Model switch!
                                    foreach (Entity players in Players)
                                    {
                                        /*
                                        var splash = HudElem.CreateFontString(players, "hudlarge", 2.0f);
                                        splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 80);
                                        var splashname = HudElem.CreateFontString(players, "hudlarge", 2.0f);
                                        splashname.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 100);
                                        string team = player.GetField<string>("sessionteam");
                                        foreach (Entity entity in Players)
                                        {
                                            if (entity.GetField<string>("sessionteam") == team)
                                            {
                                                splash.SetText("Juggernaut");
                                                AfterDelay(3000, () => splash.SetText(""));
                                                splashname.SetText("^2" + player.GetField<string>("name"));
                                                AfterDelay(3000, () => splashname.SetText(""));
                                                entity.Call("playlocalsound", "mp_card_slide");
                                                //TODO: fix Slide-in sound
                                            }
                                            if (entity.GetField<string>("sessionteam") != team)
                                            {
                                                splash.SetText("Juggernaut");
                                                AfterDelay(3000, () => splash.SetText(""));
                                                splashname.SetText("^1" + player.GetField<string>("name"));
                                                AfterDelay(3000, () => splashname.SetText(""));
                                                entity.Call("playlocalsound", "mp_card_slide");
                                                //TODO: fix Slide-in sound
                                            }
                                        }
                                         */
                                        KSSplash(player, players, "Juggernaut");
                                    }
                                }
                                else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                {
                                    AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                    player.TakeWeapon("bomb_site_mp");
                                }
                            });
                    }
                    if (Killstreak == "jugg_recon" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                            {
                                player.Call("freezeControls", false);
                                crate.Call("delete");
                                player.TakeAllWeapons();
                                player.GiveWeapon("iw5_m60jugg_mp");
                                player.GiveWeapon("m320_mp");
                                player.Call("setmovespeedscale", new Parameter((float)0.65));
                                AfterDelay(100, () =>
                                    player.SwitchToWeaponImmediate("iw5_m60jugg_mp"));
                                player.SetPerk("specialty_radarjuggernaut", true, true);
                                player.SetPerk("specialty_paint", false, true);//might not work without setting codePerk to false, else we'll have to find another way with code and OnPlayerDamage
                                player.SetPerk("specialty_coldblooded", true, true);
                                player.SetPerk("specialty_stalker", true, true);
                                player.SetField("maxhealth", 1250);
                                player.Health = 1250;
                                HudElem JuggOverlay = HudElem.CreateIcon(player, "juggernaut_overlay_alpha_col", 1920, 1080);//TODO: Find Jugg Overlay IMG Name
                                JuggOverlay.SetPoint("CENTER", "CENTER", 0, 0);
                                JuggOverlay.Alpha = 0;
                                OnInterval(100, () =>
                                {
                                    if (!player.IsAlive)
                                    {
                                        JuggOverlay.Alpha = 1;
                                        return true;
                                    }
                                    else return true;
                                });
                                //TODO: add any others!
                                /*
                                var splash = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 40);
                                var splashname = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 30);
                                string team = player.GetField<string>("sessionteam");
                                foreach (Entity entity in Players)
                                {
                                    if (entity.GetField<string>("sessionteam") == team)
                                    {
                                        splash.SetText("Juggernaut!");
                                        AfterDelay(3000, () => splash.SetText(""));
                                        splashname.SetText("^2" + player.GetField<string>("name"));
                                        AfterDelay(3000, () => splashname.SetText(""));
                                        entity.Call("playlocalsound", "mp_cardslide_v6");
                                        //TODO: fix Slide-in sound
                                    }
                                    if (entity.GetField<string>("sessionteam") != team)
                                    {
                                        splash.SetText("Juggernaut!");
                                        AfterDelay(3000, () => splash.SetText(""));
                                        splashname.SetText("^1" + player.GetField<string>("name"));
                                        AfterDelay(3000, () => splashname.SetText(""));
                                        entity.Call("playlocalsound", "mp_cardslide_v6");
                                        //TODO: fix Slide-in sound
                                    }
                                }
                                 */
                                foreach (Entity players in Players)
                                KSSplash(player, players, "Juggernaut");
                            }
                        });
                    }
                    if (Killstreak == "jugg_recon" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                //PLACEHOLDER, DO NOT USE IN GAME YET
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "ammo" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 7);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                    if (Killstreak == "ammo" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                    {
                        player.Call("iprintlnbold", "Capturing...");
                        player.GiveWeapon("bomb_site_mp");
                        player.SwitchToWeaponImmediate("bomb_site_mp");
                        player.Call("freezeControls", true);
                        AfterDelay(2500, () =>
                        {
                            if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive)
                            {
                                player.Call("freezeControls", false);
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                crate.Call("delete");
                                int Streak = player.GetField<int>("killstreak");
                                player.SetField("killstreak", 7);
                                CheckStreak(player);
                                player.SetField("killstreak", Streak);
                            }
                            else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive)
                            {
                                AfterDelay(50, () =>
                                        player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon")));
                                player.TakeWeapon("bomb_site_mp");
                                player.Call("freezeControls", false);
                            }
                        });
                    }
                });
        }
        public void WatchJuggCrate(Entity ent, Entity crate, string JuggType)
        {
            AfterDelay(90000, () =>
            {
                crate.Call("delete");
            });
                OnInterval(100, () =>
                {
                bool _changed = false;
                foreach (Entity players in Players)
                {
                players.Call("notifyonplayercommand", "triggeruse", "+activate");
                HudElem message = HudElem.CreateFontString(ent, "hudbig", 0.6f);
                message.SetPoint("CENTER", "CENTER", 0, 150);
                if (players.Origin.DistanceTo(crate.Origin) < 85 || ent.Origin.DistanceTo(crate.Origin) < 85)
                    {
                        switch (JuggType)
                        {
                            case "GL":
                                message.SetText(JuggText("GL"));
                                break;
                            case "Recon":
                                message.SetText(JuggText("Recon"));
                                break;
                        }
                        _changed = true;
                    }
                    if (!_changed)
                    {
                        message.SetText("");
                    }
                    return true;
                   }
                return true;
                });
                    ent.OnNotify("triggeruse", (player) =>
                        {
                            if (JuggType == "GL" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                            {
                                player.Call("iprintlnbold", "Capturing...");
                                player.GiveWeapon("bomb_site_mp");
                                player.SwitchToWeaponImmediate("bomb_site_mp");
                                player.Call("freezeControls", true);
                                AfterDelay(500, () =>
                                    {
                                        if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                        {
                                            player.Call("freezeControls", false);
                                            crate.Call("delete");
                                            player.TakeAllWeapons();
                                            player.GiveWeapon("gl_mp");
                                            player.Call("setmovespeedscale", new Parameter((float)0.65));
                                            AfterDelay(100, () =>
                                                player.SwitchToWeaponImmediate("gl_mp"));
                                            AfterDelay(200, () =>//Failsafe for swap bug
                                                player.SwitchToWeaponImmediate("gl_mp"));
                                            OnInterval(50, () =>
                                                {
                                                    if (player.IsAlive)
                                                        player.Call("setweaponammostock", "gl_mp", 2);
                                                    return true;
                                                });
                                            player.SetPerk("specialty_radarjuggernaut", true, true);
                                            player.SetPerk("specialty_blindeye", true, true);
                                            player.SetPerk("_specialty_blastshield", true, true);
                                            player.SetPerk("specialty_bulletaccuracy", true, true);
                                            player.SetField("maxhealth", 1250);
                                            player.Health = 1250;
                                            var JuggOverlay = HudElem.CreateIcon(ent, "juggernaut_overlay_alpha_col", 1920, 1080);
                                            JuggOverlay.SetPoint("CENTER", "CENTER", 0, 0);
                                            JuggOverlay.Alpha = 1;
                                            OnInterval(100, () =>
                                                {
                                                    if (!player.IsAlive)
                                                    {
                                                        JuggOverlay.Alpha = 0;
                                                        return true;
                                                    }
                                                    else return true;
                                                });
                                            //TODO: add Jugg Model switch!
                                            foreach (Entity players in Players)
                                            {
                                            var splash = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 80);
                                            var splashname = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splashname.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 100);
                                            string team = player.GetField<string>("sessionteam");
                                            foreach (Entity entity in Players)
                                            {
                                                if (entity.GetField<string>("sessionteam") == team)
                                                {
                                                    splash.SetText("Juggernaut");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^2" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                                if (entity.GetField<string>("sessionteam") != team)
                                                {
                                                    splash.SetText("Juggernaut");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^1" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                            }
                                            }
                                        }
                                        else if (player.Origin.DistanceTo(crate.Origin) > 85 || !player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                        {
                                            AfterDelay(50, () =>
                                                player.SwitchToWeapon(ent.GetField<string>("lastDroppableWeapon")));
                                            player.TakeWeapon("bomb_site_mp");
                                        }
                                    });
                            }
                            if (JuggType == "GL" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                            {
                                player.Call("iprintlnbold", "Capturing...");
                                player.GiveWeapon("bomb_site_mp");
                                player.SwitchToWeaponImmediate("bomb_site_mp");
                                player.Call("freezeControls", true);
                                AfterDelay(2500, () =>
                                    {
                                        if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive && crate.GetField<string>("owner") != player.GetField<string>("name"))
                                        {
                                            player.Call("freezeControls", false);
                                            crate.Call("delete");
                                            player.TakeAllWeapons();
                                            player.GiveWeapon("gl_mp");
                                            player.Call("setmovespeedscale", new Parameter((float)0.65));
                                            AfterDelay(100, () =>
                                                player.SwitchToWeaponImmediate("gl_mp"));
                                            OnInterval(50, () =>
                                            {
                                                if (player.IsAlive)
                                                    player.Call("setweaponammostock", "gl_mp", 1);
                                                return true;
                                            });
                                            player.SetPerk("specialty_radarjuggernaut", true, true);
                                            player.SetPerk("specialty_blindeye", true, true);
                                            player.SetPerk("_specialty_blastshield", true, true);
                                            player.SetPerk("specialty_bulletaccuracy", true, true);
                                            player.SetField("maxhealth", 1250);
                                            player.Health = 1250;
                                            var JuggOverlay = HudElem.CreateIcon(ent, "juggernaut_overlay_alpha_col", 1920, 1080);//TODO: Find Jugg Overlay IMG Name
                                            JuggOverlay.SetPoint("CENTER", "CENTER", 0, 0);
                                            JuggOverlay.Alpha = 1;
                                            OnInterval(100, () =>
                                            {
                                                if (!player.IsAlive)
                                                {
                                                    JuggOverlay.Alpha = 0;
                                                    return true;
                                                }
                                                else return false;
                                            });
                                            //TODO: add any others!
                                            var splash = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 80);
                                            var splashname = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splashname.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 100);
                                            string team = player.GetField<string>("sessionteam");
                                            foreach (Entity entity in Players)
                                            {
                                                if (entity.GetField<string>("sessionteam") == team)
                                                {
                                                    splash.SetText("Juggernaut");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^2" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                                if (entity.GetField<string>("sessionteam") != team)
                                                {
                                                    splash.SetText("Juggernaut");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^1" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                            }
                                        }
                                        if (JuggType == "Recon" && ent.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                        {
                                player.Call("iprintlnbold", "Capturing...");
                                player.GiveWeapon("bomb_site_mp");
                                player.SwitchToWeaponImmediate("bomb_site_mp");
                                player.Call("freezeControls", true);
                                AfterDelay(2500, () =>
                                    {
                                        if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                        {
                                            player.Call("freezeControls", false);
                                            crate.Call("delete");
                                            player.TakeAllWeapons();
                                            player.GiveWeapon("iw5_m60jugg_mp");
                                            player.GiveWeapon("m320_mp");
                                            player.Call("setmovespeedscale", new Parameter((float)0.65));
                                            AfterDelay(100, () =>
                                                player.SwitchToWeaponImmediate("iw5_m60jugg_mp"));
                                            player.SetPerk("specialty_radarjuggernaut", true, true);
                                            player.SetPerk("specialty_paint", false, true);//might not work without setting codePerk to false, else we'll have to find another way with code and OnPlayerDamage
                                            player.SetPerk("specialty_coldblooded", true, true);
                                            player.SetPerk("specialty_stalker", true, true);
                                            player.SetField("maxhealth", 1250);
                                            player.Health = 1250;
                                            var JuggOverlay = HudElem.CreateIcon(ent, "juggernaut_overlay_alpha_col", 1920, 1080);//TODO: Find Jugg Overlay IMG Name
                                            JuggOverlay.SetPoint("CENTER", "CENTER", 0, 0);
                                            JuggOverlay.Alpha = 0;
                                            OnInterval(100, () =>
                                            {
                                                if (!player.IsAlive)
                                                {
                                                    JuggOverlay.Alpha = 1;
                                                    return true;
                                                }
                                                else return true;
                                            });
                                            //TODO: add any others!
                                            var splash = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 40);
                                            var splashname = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 30);
                                            string team = player.GetField<string>("sessionteam");
                                            foreach (Entity entity in Players)
                                            {
                                                if (entity.GetField<string>("sessionteam") == team)
                                                {
                                                    splash.SetText("Juggernaut!");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^2" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                                if (entity.GetField<string>("sessionteam") != team)
                                                {
                                                    splash.SetText("Juggernaut!");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^1" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                            }
                                        }
                                    });
                                        }
                                        if (JuggType == "Recon" && player.Origin.DistanceTo(crate.Origin) < 85 && crate.GetField<string>("owner") != player.GetField<string>("name"))
                                        {
                                player.Call("iprintlnbold", "Capturing...");
                                player.GiveWeapon("bomb_site_mp");
                                player.SwitchToWeaponImmediate("bomb_site_mp");
                                player.Call("freezeControls", true);
                                AfterDelay(2500, () =>
                                    {
                                        if (player.Origin.DistanceTo(crate.Origin) < 85 && player.IsAlive && crate.GetField<string>("owner") == player.GetField<string>("name"))
                                        {
                                            player.Call("freezeControls", false);
                                            crate.Call("delete");
                                            player.TakeAllWeapons();
                                            player.GiveWeapon("iw5_m60jugg_mp");
                                            player.GiveWeapon("m320_mp");
                                            player.Call("setmovespeedscale", new Parameter((float)0.65));
                                            AfterDelay(100, () =>
                                                player.SwitchToWeaponImmediate("iw5_m60jugg_mp"));
                                            player.SetPerk("specialty_radarjuggernaut", true, true);
                                            player.SetPerk("specialty_paint", false, true);//might not work without setting codePerk to false, else we'll have to find another way with code and OnPlayerDamage
                                            player.SetPerk("specialty_coldblooded", true, true);
                                            player.SetPerk("specialty_stalker", true, true);
                                            player.SetField("maxhealth", 1250);
                                            player.Health = 1250;
                                            var JuggOverlay = HudElem.CreateIcon(ent, "juggernaut_overlay_alpha_col", 1920, 1080);//TODO: Find Jugg Overlay IMG Name
                                            JuggOverlay.SetPoint("CENTER", "CENTER", 0, 0);
                                            JuggOverlay.Alpha = 0;
                                            OnInterval(100, () =>
                                            {
                                                if (!player.IsAlive)
                                                {
                                                    JuggOverlay.Alpha = 1;
                                                    return true;
                                                }
                                                else return true;
                                            });
                                            //TODO: add any others!
                                            var splash = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 40);
                                            var splashname = HudElem.CreateFontString(ent, "hudlarge", 2.0f);
                                            splash.SetPoint("TOPRIGHT", "TOPRIGHT", 0, 30);
                                            string team = player.GetField<string>("sessionteam");
                                            foreach (Entity entity in Players)
                                            {
                                                if (entity.GetField<string>("sessionteam") == team)
                                                {
                                                    splash.SetText("Juggernaut");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^2" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                                if (entity.GetField<string>("sessionteam") != team)
                                                {
                                                    splash.SetText("Juggernaut");
                                                    AfterDelay(3000, () => splash.SetText(""));
                                                    splashname.SetText("^1" + player.GetField<string>("name"));
                                                    AfterDelay(3000, () => splashname.SetText(""));
                                                    entity.Call("playlocalsound", "mp_cardslide_v6");
                                                    //TODO: fix Slide-in sound
                                                }
                                            }
                                        }
                                    });
                                        }
                                    });
                            }
                        });
        }
        public string JuggText(string JuggType)
        {
            if (JuggType == "GL")
                return "Press and Hold ^3[{+activate}]^7 for Juggernaut";
            if (JuggType == "Recon")
                return "Press and Hold ^3[{+activate}]^7 for Juggernaut";
            //In Case we want to seperate these strings for later use
            return "";
        }
        public string CarePackText(Entity ent, string Killstreak)
        {
            if (Killstreak == "team_ammo_refill")
                return "Press and hold ^3[{+activate}]^7 for Team Ammo Refill";
            if (Killstreak == "uav_strike")
                return "Press and hold ^3[{+activate}]^7 for UAV Strike";
            if (Killstreak == "gl_mp")
                return "Press and Hold ^3[{+activate}]^7 for Grenade Launcher";
            if (Killstreak == "super_airstrike")
                return "Press and Hold ^3[{+activate}]^7 for Super Airstrike";
            if (Killstreak == "a10")
                return "Press and hold ^3[{+activate}]^7 for A-10 Support";
            if (Killstreak == "mobile_mortars")
                return "Press and hold ^3[{+activate}]^7 for Mobile Mortar";
            if (Killstreak == "aastrike")
                return "Press and Hold ^3[{+activate}]^7 for Air Superiority";
            if (Killstreak == "harriers")
                return "Press and Hold ^3[{+activate}]^7 for Harriers";
            if (Killstreak == "aamissile")
                return "Press and hold ^3[{+activate}]^7 for Anti-Air Missile";
            if (Killstreak == "ammo")
                return "Press and Hold ^3[{+activate}]^7 for Ammo";
            return "";
        }
        public void CallSAirStrike(Entity ent, Vector3 location, int locationYaw)
        {
            string team = ent.GetField<string>("sessionteam");
            foreach (Entity entity in Players)
            {
                if (Call<string>("getdvar", "g_gametype") == "dm")
                    entity.Call("playlocalsound", "RU_KS_ast_inbound");
                else 
                {
                if (entity.GetField<string>("sessionteam") == team)//Friendly call-in
                {
                    entity.Call("playlocalsound", "US_KS_ast_inbound");
                }
                if (entity.GetField<string>("sessionteam") != team)
                {
                    entity.Call("playlocalsound", "RU_KS_ast_inbound");//Enemy
                }
                    }
            }
            SpawnSAJet(ent, location, locationYaw);
        }
        public void SpawnAirSup(Entity ent)
        {
            //Entity jet1 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));//TODO: get values for jetspawn on ALL maps. Dome will be Default for now
            //Entity jet2 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2024.995f, 800.2477f)));
            //Entity jet3 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(0, 0, 0)));
            //Entity jet4 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(0, 0, 0)));//all 4 spawn on the open side of the map, East.GRAB SPAWNS FOR THE LAST 2 PLANES!!! Should be to the left of current.
            Entity jet1 = Call<Entity>(367, ent, "script_model", new Vector3(-6664.267f, -4125.583f, 961.1196f), "hud_minimap_harrier_green", "hud_minimap_harrier_red");
            Entity jet2 = Call<Entity>(367, ent, "script_model", new Vector3(-6524.267f, -4125.583f, 911.1196f), "", "");//+140, 0, -50
            //Get other spawns for these two
            Entity jet3 = Call<Entity>(367, ent, "script_model", new Vector3(-6825.954f, 11921.19f, 1066.993f), "hud_minimap_harrier_green", "hud_minimap_harrier_red");
            Entity jet4 = Call<Entity>(367, ent, "script_model", new Vector3(-6685.954f, 11921.19f, 1016.993f), "", "");
            jet1.Call("setmodel", "vehicle_phantom_ray");
            jet2.Call("setmodel", "vehicle_phantom_ray");
            jet3.Call("setmodel", "vehicle_phantom_ray");
            jet4.Call("setmodel", "vehicle_phantom_ray");
            jet1.SetField("angles", new Parameter(new Vector3(0, 43, 0)));//TODO: Get proper angles for ALL maps. Dome will be default. Angles will be a set int
            jet2.SetField("angles", new Parameter(new Vector3(0, 43, 0)));
            jet3.SetField("angles", new Parameter(new Vector3(0, -45, 0)));// Get these to be angled correctly for the new spawn
            jet4.SetField("angles", new Parameter(new Vector3(0, -45, 0)));
            jet3.Call("hide");
            jet4.Call("hide");
            AirSupJetFlyBy(ent, jet1, jet2, jet3, jet4);
        }
        public void AirSupJetFlyBy(Entity ent, Entity jet1, Entity jet2, Entity jet3, Entity jet4)
        {
            jet1.Call("playloopsound", "veh_aastrike_flyover_loop");
            jet1.Call(33399, new Parameter(new Vector3(7665.359f, 6960.892f, 1104.184f)), 10); //moveto, TODO: get proper location and speed for flyin. Dome is Default.
            jet2.Call(33399, new Parameter(new Vector3(7805.359f, 6960.892f, 1054.184f)), 10); //Should move alongside Jet1
            //jet1.Call("", "");
            //jet2.Call("", "");
            //Dont know what these are for, but they may come in useful if there are missing calls.
            AfterDelay(8000, () =>
                {
                    jet3.Call("show");
                    jet4.Call("show");
                    jet3.Call(33399, new Parameter(new Vector3(7540.645f, -6032.632f, 804.1209f)), 10); //moveto, TODO: get proper location and speed for flyin. Dome is Default.
                    jet4.Call(33399, new Parameter(new Vector3(7680.645f, -6032.632f, 754.1209f)), 10); //Should move alongside Jet3
                    jet3.Call("playloopsound", "veh_aastrike_flyover_loop");
                    AfterDelay(2000, () =>
                        {
                            jet1.Call("delete");
                            jet2.Call("delete");
                        });
                });
            AfterDelay(18000, () =>
                {
                    jet3.Call("delete");
                    jet4.Call("delete");
                });
            //Make a targeting mechanism for Killstreaks in the air(UAV, AC130, Reaper, etc.) and use similar method from Harrier/A10 targeting but add an Entity Bank for Destroyable Aircraft only and use that instead of the Players Bank.
        }
        public void SpawnSAJet(Entity ent, Vector3 location, int directionYaw)
        {
            //Entity jet = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));
            Entity jet = Call<Entity>(367, ent, "script_model", new Vector3(-10421.41f, 2014.995f, 807.2477f), "compass_objpoint_airstrike_friendly", "compass_objpoint_airstrike_busy");
            jet.SetField("angles", new Vector3(0, directionYaw, 0));
            //Entity jet2 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));
            Entity jet2 = Call<Entity>(367, ent, "script_model", new Vector3(-10421.41f, 2014.995f, 807.2477f), "compass_objpoint_airstrike_friendly", "compass_objpoint_airstrike_busy");
            jet2.SetField("angles", new Vector3(0, directionYaw, 0));
            //Entity jet3 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));
            Entity jet3 = Call<Entity>(367, ent, "script_model", new Vector3(-10421.41f, 2014.995f, 807.2477f), "compass_objpoint_airstrike_friendly", "compass_objpoint_airstrike_busy");
            jet3.SetField("angles", new Vector3(0, directionYaw, 0));
            //Entity jet4 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));
            Entity jet4 = Call<Entity>(367, ent, "script_model", new Vector3(-10421.41f, 2014.995f, 807.2477f), "compass_objpoint_airstrike_friendly", "compass_objpoint_airstrike_busy");
            jet4.SetField("angles", new Vector3(0, directionYaw, 0));
            /*
            string team = ent.GetField<string>("sessionteam");
            foreach (Entity entity in Players)
            {
                if (entity.GetField<string>("sessionteam") == team)//Friendly
                {
                    jet.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
                    jet2.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
                    jet2.Call("hide");
                    jet3.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
                    jet3.Call("hide");
                    jet4.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
                    jet4.Call("hide");
                }
                if (entity.GetField<string>("sessionteam") != team)//Enemy
                {
                    jet.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
                    jet2.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
                    jet2.Call("hide");
                    jet3.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
                    jet3.Call("hide");
                    jet4.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
                    jet4.Call("hide");
                }
            }
             */
            jet.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
            jet2.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
            jet2.Call("hide");
            jet3.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
            jet3.Call("hide");
            jet4.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
            jet4.Call("hide");
            AfterDelay(500, () =>
                {
                    Call(305, AfterburnerFX, jet, "tag_engine_right");
                    Call(305, AfterburnerFX, jet, "tag_engine_left");
                });
            AfterDelay(500, () =>
                {
                    Call(305, ContrailFX, jet, "tag_right_wingtip");
                    Call(305, ContrailFX, jet, "tag_left_wingtip");
                });

            //TODO: Add FX^^?, Fix killcam bug(temp is disable KCs), fix Announce sound and Achieve sound
            SJetFlyBy(ent, jet, jet2, jet3, jet4);
        }
        public void SpawnHarrierJet(Entity ent, Vector3 location)
        {
            Vector3 forward = Call<Vector3>(247, (location + new Vector3(10000, 0, 0)) - (location - new Vector3(10000, 0, 0)));
            Entity jet = Call<Entity>(367, ent, "script_model", location - new Vector3(10000, 0, 0), "compass_objpoint_airstrike_friendly", "compass_objpoint_airstrike_busy");
            //Entity jet = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));
            jet.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
            jet.SetField("angles", forward);//TODO: Get proper angles for ALL maps. Dome will be default.
            //Entity jet2 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));
            Entity jet2 = Call<Entity>(367, ent, "script_model", location - new Vector3(10000, 0, 0), "compass_objpoint_airstrike_friendly", "compass_objpoint_airstrike_busy");
            jet2.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
            jet2.SetField("angles", forward);//TODO: Get proper angles for ALL maps. Dome will be default.
            jet2.Call("hide");
            AfterDelay(200, () =>
            {
                Call(305, AfterburnerFX, jet, "tag_engine_right");
                Call(305, AfterburnerFX, jet, "tag_engine_left");
                Call(305, AfterburnerFX, jet2, "tag_engine_right");
                Call(305, AfterburnerFX, jet2, "tag_engine_left");
            });
            AfterDelay(200, () =>
            {
                Call(305, ContrailFX, jet, "tag_right_wingtip");
                Call(305, ContrailFX, jet, "tag_left_wingtip");
                Call(305, ContrailFX, jet2, "tag_right_wingtip");
                Call(305, ContrailFX, jet2, "tag_left_wingtip");
            });
            HJetFlyBy(ent, jet, jet2, location);
        }
        public void SJetFlyBy(Entity ent, Entity jet, Entity jet2, Entity jet3, Entity jet4)
        {
            //int curObjID = 31 - _mapCount++;
            Entity Bomb = Call<Entity>("spawn", "script_model", jet.Origin);
            //testing Gravity of this from IW
            Bomb.Call(33403, (jet.Call<Vector3>(252, jet)) * 10500, 3);
            Bomb.Call("setmodel", "projectile_cbu97_clusterbomb");//Model
            Vector3 endPoint = jet.Origin + Call<Vector3>("AnglesToForward", jet.GetField<Vector3>("angles")) * 24000;
            jet.Call(33399, endPoint, 5); //moveto, TODO: get proper location and speed for flyin. Dome is Default.
            jet.Call("playloopsound", "veh_mig29_dist_loop");
            AfterDelay(1100, () =>
            {
                //TODO: add proper bomb to fall(artillery_mp?), get proper angles to drop, get proper timing for AfterDelay, Finalize
            });
            AfterDelay(3000, () =>
                {
                    jet2.Call("show");
                    jet2.Call(33399, endPoint, 5); //Strike again
                    jet2.Call("playloopsound", "veh_mig29_dist_loop");
                    AfterDelay(900, () =>
                        {
                            
                        });
                    AfterDelay(1000, () =>
                        {
                            jet.Call("hide");
                            jet.Call("delete");
                        });
                });
            AfterDelay(6000, () =>
            {
                jet3.Call("show");
                jet3.Call(33399, endPoint, 5); //Strike again
                jet3.Call("playloopsound", "veh_mig29_dist_loop");
                AfterDelay(900, () =>
                {
                    
                });
                AfterDelay(1000, () =>
                {
                    jet2.Call("hide");
                    jet2.Call("delete");
                });
            });
            //Pause here as shown by IW code
            AfterDelay(10000, () =>
            {
                jet4.Call("show");
                jet4.Call(33399, endPoint, 5); //Strike again
                jet4.Call("playloopsound", "veh_mig29_dist_loop");
                AfterDelay(900, () =>
                {
                    
                });
                AfterDelay(1000, () =>
                {
                    jet3.Call("hide");
                    jet3.Call("delete");
                });
                AfterDelay(4000, () =>
                {
                    jet4.Call("hide");
                    jet4.Call("delete");
                });
            });
        }
        public void HJetFlyBy(Entity ent, Entity jet, Entity jet2, Vector3 location)
        {
            AfterDelay(1500, () =>
                {
                    Vector3 pathStart = location - new Vector3(10000, 0, 0);
                    Vector3 pathGoal = location + new Vector3(10000, 0, 0);
                    //TODO: Add bulletTrace(88) and replace javelin with radiusDamage
                    jet.Call(33399, location, 5); //moveto, TODO: get proper location and speed for flyin. Dome is Default.
                    jet.Call("playloopsound", "veh_mig29_dist_loop");
                    AfterDelay(1600, () =>
                    {
                        Call("magicbullet", "javelin_mp", jet.Origin - new Vector3(0, 0, 5), location - new Vector3(0, 0, 850), ent);
                        AfterDelay(100, () =>
                            Call("magicbullet", "javelin_mp", jet.Origin - new Vector3(0, 0, 5), location - new Vector3(0, 0, 850), ent));
                        AfterDelay(200, () =>
                            Call("magicbullet", "javelin_mp", jet.Origin - new Vector3(0, 0, 5), location - new Vector3(0, 0, 850), ent));
                    });
                    AfterDelay(3500, () =>
            {
                jet2.Call(33399, pathGoal, 5);//second strike
                jet2.Call("show");
                jet2.Call("playloopsound", "veh_mig29_dist_loop");
                AfterDelay(1500, () =>
                {
                    jet.Call("delete");
                    Call("magicbullet", "artillery_mp", jet2.Origin - new Vector3(0, 0, 5), location - new Vector3(0, 0, 850), ent);
                    AfterDelay(100, () =>
                        Call("magicbullet", "artillery_mp", jet2.Origin - new Vector3(0, 0, 5), location - new Vector3(0, 0, 850), ent));
                    AfterDelay(200, () =>
                        Call("magicbullet", "artillery_mp", jet2.Origin - new Vector3(0, 0, 5), location - new Vector3(0, 0, 850), ent));
                });
            });
                    AfterDelay(6500, () =>
            {
                AfterDelay(2000, () =>
                    jet2.Call("delete"));
                //if (ent.GetField<string>("sessionteam") == "allies")
                //{
                //Entity harrier = Call<Entity>(369, ent, pathStart, forward, "harrier_mp", "vehicle_av8b_harrier_jet_mp");//TODO: Get this to become moveable without depleting function
                Entity harrier = Call<Entity>(367, ent, "script_model", pathStart, "hud_minimap_harrier_green", "hud_minimap_harrier_red");
                harrier.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
                //}
                //else
                //{
                //Entity harrier = Call<Entity>(369, ent, pathStart, forward, "harrier_mp", "vehicle_av8b_harrier_jet_opfor_mp");//TODO: get this to function
                //}
                //harrier.SetField("speed", 250);
                //harrier.SetField("accel", 175);
                harrier.Health = 3000;
                harrier.SetField("maxhealth", harrier.Health);
                harrier.Call("setcandamage", true);
                AfterDelay(200, () =>
                    {
                        Call(305, AfterburnerFX, harrier, "tag_engine_right");
                        Call(305, AfterburnerFX, harrier, "tag_engine_left");
                    });
                AfterDelay(200, () =>
                    {
                        Call(305, ContrailFX, harrier, "tag_right_wingtip");
                        Call(305, ContrailFX, harrier, "tag_left_wingtip");
                    });
                AfterDelay(200, () =>
                    Call(305, WingTipLight_Red, harrier, "tag_light_L_wing"));
                AfterDelay(200, () =>
                    Call(305, WingTipLight_Red, harrier, "tag_light_R_wing"));
                AfterDelay(200, () =>
                    Call(305, WingTipLight_Red, harrier, "tag_light_belly"));
                AfterDelay(200, () =>
                    Call(305, WingTipLight_Red, harrier, "tag_light_tail"));
                harrier.SetField("missiles", 2);
                harrier.OnNotify("damage", (entity, damage, attacker, direction_vec, point, meansOfDeath, modelName, partName, tagName, iDFlags, weapon) =>
                    {

                    });
                harrier.SetField("angles", Call<Vector3>(247, location - harrier.Origin));
                harrier.Call("playloopsound", "veh_aastrike_flyover_loop");
                harrier.Call(33399, location, 9, 0.1f, 3);
                Entity Owner = ent;
                OnInterval(7500, () =>
                {
                    if (harrier.HasField("Destroyed"))
                    {
                        harrier.Call("stoploopsound");
                        harrier.Call("playsound", "harrier_fly_away");
                        harrier.Call(33406, Call<Vector3>(247, pathGoal - harrier.Origin), 3.5f, 1, 1);
                        AfterDelay(3400, () =>
                        {
                            harrier.Call(33399, new Parameter(pathGoal), 6.5f, 2.5f, 0.1f);//Leave
                            AfterDelay(7500, () =>
                                harrier.Call("delete"));
                        });
                        return false;
                    }
                    foreach (Entity players in Players)
                    {
                        Entity target = null;
                        if (Call<int>(116, harrier.Call<Vector3>("gettagorigin", "tag_flash"), players.Call<Vector3>("getTagOrigin", "j_head"), false, Owner) == 1)
                        {
                            if (target != null)
                            {
                                if (Call<int>("closer", harrier.Origin, players.Call<Vector3>("getTagOrigin", "j_head"), target.Call<Vector3>("getTagOrigin", "j_head")) == 1)
                                {
                                    target = players;
                                    ShootHarrier(target, harrier, ent);
                                }
                            }
                            else
                            {
                                target = players;
                                ShootHarrier(target, harrier, ent);
                            }
                        }
                    }
                    return true;
                });
                AfterDelay(66000, () =>
                {
                    //harrier.Call(33408, 50, 3.5f, 1, 1);//slow rotate
                    harrier.SetField("Destroyed", true);
                });
            });
                });
        }
        public void ShootHarrier(Entity target, Entity harrier, Entity ent)
        {
            Vector3 jetpos = harrier.Call<Vector3>("gettagorigin", "tag_flash");
            int? Shot;
            //if (harrier.GetField<int>("missiles") > 0) Shot = new Random().Next(2);
            Shot = new Random().Next(1);
            Vector3 newDir = Call<Vector3>(247, target.Origin - harrier.Origin);
            harrier.Call(33406, new Vector3(0, newDir.Y, 0), 3.5f, 1, 1);//slow rotate
            AfterDelay(1500, () =>
            {
                switch (Shot)
                {
                    case 0://Regular shots
                        {
                            Log.Write(LogLevel.All, "Shooting gun...");
                            harrier.Call("stoploopsound");
                            harrier.Call("playloopsound", "weap_cobra_20mm_fire_npc");
                            Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent);
                            AfterDelay(50, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(100, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(150, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(200, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(250, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(300, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(350, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(400, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(450, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(500, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(550, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(600, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(650, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(700, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(750, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(800, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(850, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(900, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(950, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(1000, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(1050, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(1100, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(1150, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            AfterDelay(1200, () =>
                                Call("magicbullet", "cobra_20mm_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent));
                            harrier.Call("stoploopsound");
                            harrier.Call("playloopsound", "veh_aastrike_flyover_loop");
                            break;
                        }
                    case 1://Missile for variety
                        {
                            Call("magicbullet", "harrier_missile_mp", jetpos, target.Call<Vector3>("getTagOrigin", "j_spine4"), ent);
                            harrier.SetField("missiles", harrier.GetField<int>("missiles") - 1);
                            break;
                        }
                }
            });
        }
        public void SpawnA10(Entity player, Vector3 loc, int dir)
        {
            foreach (Entity entity in Players)
                KSSplash(player, entity, "A-10 Support");
            //Entity a10 = Call<Entity>("spawn", "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)));//TODO: get values for jetspawn on ALL maps. Dome will be Default
            Entity a10 = Call<Entity>(367, player, "script_model", new Parameter(new Vector3(-10421.41f, 2014.995f, 807.2477f)), "hud_minimap_harrier_green", "hud_minimap_harrier_red");

            a10.SetField("angles", new Parameter(new Vector3(0, 0, 0)));//TODO: Get proper angles for ALL maps. Dome will be default.
            string team = player.GetField<string>("sessionteam");
            foreach (Entity entity in Players)
            {
                if (entity.GetField<string>("sessionteam") == team)//Friendly
                {
                    a10.Call("setmodel", "vehicle_av8b_harrier_jet_mp");
                }
                if (entity.GetField<string>("sessionteam") != team)//Enemy
                {
                    a10.Call("setmodel", "vehicle_av8b_harrier_jet_opfor_mp");
                }
            }
            A10FlyBy(player, a10);
            a10.Call("playloopsound", "veh_mig29_dist_loop");
        }
        public void A10FlyBy(Entity player, Entity a10)
        {
            //TODO: FINISH Anim!
            a10.Call(33399, new Parameter(new Vector3(-1636.183f, 887.6493f, 379.3764f)), 5); //moveto, TODO: get proper location and speed for flyin. A10 needs special points to 'dip' to the map a la BOII. Multiple moveto's will have to be implemented. Dome is Default.
            string Owner = player.Name;
            string team = player.GetField<string>("sessionteam");
            //rotateYaw/Pitch/Roll uses arguments (amount of Degrees, speed of rotation)
            //ANGLES: X(Pitch, Nose up is -), Y(Point, - is to the Right), Z(Tilt, Right wing down is +)
            AfterDelay(4000, () =>
                {
                    a10.Call(33399, new Parameter(new Vector3(1916.828f, 945.5536f, 328.6272f)), 5);
                    a10.SetField("angles", new Parameter(new Vector3(0, -5, 0)));
                });
            AfterDelay(8000, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(6838.965f, 584.779f, 1278.554f)), 5);
                a10.Call("rotatepitch", -10, 0.6f, 0.3f, 0.3f);
            });
            AfterDelay(13000, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(7651.085f, -690.4987f, 1212.197f)), 5);//Turn Around
                //a10.Call("rotateyaw", -45, 0.6f);
                //a10.Call("rotatepitch", -35, 0.6f);
                //a10.Call("rotateroll", 45, 0.6f);
                a10.Call("rotateto", new Vector3(-35, 0, 45), 5, 2.5f, 2.5f);
            });
            AfterDelay(17000, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(6902.055f, -1962.215f, 1165.547f)), 5);//Parrallel to map
                //Original is -90, so maybe these are additive and not set?
                //a10.Call("rotateyaw", -90, 0.6f);
                //a10.Call("rotateyaw", -45, 0.6f);//Additive copy
                //a10.Call("rotatepitch", -10, 0.6f);
                //a10.Call("rotateroll", 45, 0.6f);
                a10.Call("rotateto", new Vector3(-45, -45, 45), 5, 2.5f, 2.5f);
            });
            AfterDelay(20000, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(5315.319f, -2639.785f, 1127.972f)), 5);//Turn Around
                //a10.Call("rotateyaw", -125, 0.6f);
                //a10.Call("rotatepitch", 25, 0.6f);
                //a10.Call("rotateyaw", -35, 0.6f);
                //a10.Call("rotateroll", -20, 0.6f);
                a10.Call("rotateto", new Vector3(-25, -90, 25), 5, 2.5f, 2.5f);
            });
            AfterDelay(23000, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(3362.155f, -2170.438f, 909.8755f)), 5);//2nd Strafe
                //a10.Call("rotateyaw", -140, 0.6f);
                //a10.Call("rotatepitch", 20, 0.6f);
                //a10.Call("rotateyaw", -15, 0.6f);
                //a10.Call("rotateroll", -25, 0.6f);
                a10.Call("rotateto", new Vector3(-5, -160, 0), 5, 2.5f, 2.5f);
            });
            AfterDelay(26500, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(2005.67f, -981.3924f, 420.2338f)), 5);//Dip down for 2nd strafe
                //a10.Call("rotateyaw", -140, 0.6f);//if these are additive, this is invalid
                a10.Call("rotatepitch", -10, 0.6f);
            });
            AfterDelay(30000, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(-699.8965f, 1784.169f, 367.8614f)), 5);//flat
                a10.Call("rotatepitch", 0, 0.6f);
            });
            AfterDelay(36500, () =>
            {
                a10.Call(33399, new Parameter(new Vector3(-5788.362f, 8851.542f, 926.5215f)), 4);//Leave the map angled up
                a10.Call("rotatepitch", 10, 0.6f);
                AfterDelay(2000, () =>
                {
                    a10.Call("delete");
                });
            });
            //Add more strafes if needed
            OnInterval(550, () =>
                {
                    foreach (Entity players in Players)
                    {
                        if (players.Origin.DistanceTo(a10.Origin) < 1000 && players.GetField<string>("name") != Owner && players.GetField<string>("sessionteam") != team && StreaksInAir.Contains(a10))
                        {
                            Vector3 a10pos = a10.Origin;
                            Vector3 dest = players.Origin;
                            Entity bulletFx = Call<Entity>("spawnfx", BulletRainFX, a10.Origin);
                            Call("triggerfx", bulletFx);//May need to stop this by playing this on tag_origin and stopping at the right time as shown below.
                            //Call("playfxontag", bulletFx, a10, "tag_origin");
                            Call("magicbullet", "ac130_25mm_mp", a10pos, dest, player);
                            a10.Call("playloopsound", "pavelow_mg_loop");
                            AfterDelay(100, () =>
                                Call("magicbullet", "ac130_25mm_mp", a10pos, dest, player));
                            AfterDelay(200, () =>
                                Call("magicbullet", "ac130_25mm_mp", a10pos, dest, player));
                            AfterDelay(300, () =>
                                Call("magicbullet", "ac130_25mm_mp", a10pos, dest, player));
                            AfterDelay(400, () =>
                                Call("magicbullet", "ac130_40mm_mp", a10pos, dest, player));
                            AfterDelay(450, () =>
                            a10.Call("stoploopsound", "pavelow_mg_loop"));
                        }
                    }
                    return true;
                });
        }
        public void KSSplash(Entity caller, Entity players, string splash)
        {
            /*
            Vector3 Red = new Vector3(0.7f, 0, 0);
            Vector3 Green = new Vector3(0, 0.7f, 0);
            var splash = HudElem.CreateFontString(players, "objective", 1.5f);
            splash.SetPoint("TOPRIGHT", "TOPRIGHT", -5, 120);
            splash.GlowColor = new Vector3(0.5f, 0.5f, 0.5f);
            splash.GlowAlpha = 0.5f;
            var splashname = HudElem.CreateFontString(players, "hudbig", 0.8f);
            splashname.SetPoint("TOPRIGHT", "TOPRIGHT", -5, 100);
            string team = caller.GetField<string>("sessionteam");
            string KillstreakCaller = caller.Name;
            if (Call<string>("getdvar", "g_gametype") != "dm")
            {
                if (players.GetField<string>("sessionteam") == team)
                {
                    splashname.Color = Green;
                    splash.SetText(Splash);
                    AfterDelay(3000, () => splash.Call("destroy"));
                    splashname.SetText(KillstreakCaller);
                    AfterDelay(3000, () => splashname.Call("destroy"));
                    players.Call("playlocalsound", "mp_card_slide");
                }
                else if (players.GetField<string>("sessionteam") != team)
                {
                    splashname.Color = Red;
                    splash.SetText(Splash);
                    AfterDelay(3000, () => splash.SetText(""));
                    splashname.SetText(KillstreakCaller);
                    AfterDelay(3000, () => splashname.SetText(""));
                    players.Call("playlocalsound", "mp_card_slide");
                }
            }
            else
            {
                if (players == caller)
                {
                    splashname.Color = Green;
                    splash.SetText(Splash);
                    AfterDelay(3000, () => splash.Call("destroy"));
                    splashname.SetText(KillstreakCaller);
                    AfterDelay(3000, () => splashname.Call("destroy"));
                    players.Call("playlocalsound", "mp_card_slide");
                }
                else if (players != caller)
                {
                    splashname.Color = Red;
                    splash.SetText(Splash);
                    AfterDelay(3000, () => splash.SetText(""));
                    splashname.SetText(KillstreakCaller);
                    AfterDelay(3000, () => splashname.SetText(""));
                    players.Call("playlocalsound", "mp_card_slide");
                }
            }
             */
            //foreach (Entity players in spawnedPlayers)
            {
                //if (!players.IsAlive) return;
                players.Call(33422, caller, 5);
                players.Call(33392, splash, 1);
            }
        }
        public bool GLCheck(Entity player)
        {
            string weapon = player.CurrentWeapon;
            if (weapon != "gl_mp")
            {
                player.Call("unsetperk", "specialty_dangerclose", true);
                player.Call("unsetperk", "specialty_explosivedamage", true);
                if (player.GetField<int>("HasSOH") == 0)
                    player.Call("unsetperk", "specialty_fastreload", true);
                return false;
            }
            else return false;
        }
        public void Ammo(Entity ent)
        {
            var wep = ent.CurrentWeapon;
            ent.SetField("AmmoGotten", 0);
            ent.Call("givemaxammo", wep);
            ent.Call("givemaxammo", "frag_grenade_mp");
            ent.Call("givemaxammo", "semtex_mp");
            ent.Call("givemaxammo", "throwingknife_mp");
            ent.Call("givemaxammo", "claymore_mp");
            ent.Call("givemaxammo", "c4_mp");
            ent.Call("givemaxammo", "bouncingbetty_mp");
            ent.Call("givemaxammo", "concussion_grenade_mp");
            ent.Call("givemaxammo", "flash_grenade_mp");
            ent.Call("givemaxammo", "trophy_mp");
            ent.Call("givemaxammo", "scrambler_mp");
            ent.Call("givemaxammo", "protable_radar_mp");
            ent.Call("givemaxammo", "flare_mp");
            ent.Call("givemaxammo", "emp_grenade_mp");
        }
        private bool mayDropWeapon(string weapon)
        {
            if (weapon == "none")
                return false;

            if (weapon.Contains("ac130"))
                return false;

            string invType = Call<string>("WeaponInventoryType", weapon);
            if (invType != "primary")
                return false;

            return true;
        }
        private int _mapCount = 0;
        //public void Valkyrie(Entity ent)
        //{
        //    initRideKillstreak(ent, "predator_missile");

        //}
        //private static void SetUsingRemote(Entity ent, string remote = "")
        //{
        //    ent.Call("DisableOffhandWeapons");
        //    //ent.SetField("usingRemote", remote);
        //    ent.Notify("using_remote");
        //}
        //private void initRideKillstreak(Entity ent, string streakName = "")
        //{
        //    if (!string.IsNullOrEmpty(streakName) && (streakName == "osprey_gunner" || streakName == "remote_uav" || streakName == "remote_tank"))
        //    {
        //        ent.SetField("laptopWait", "timeout");
        //        ent.OnNotify("valkaim", e1 =>
        //            {
        //                ent.SetField("flag", 0);
        //                initRideKillstreak2(ent);
        //            });
        //    }
        //    else
        //    {
        //        ent.SetField("laptopWait", "get");
        //        //laptopWait = self waittill_any_timeout( 1.0, "disconnect", "death", "weapon_switch_started" );
        //        int counter = 0;
        //        //maybe smarter to use 50 interval?
        //        ent.OnInterval(100, entity =>
        //        {
        //            counter++;
        //            if (entity == null)
        //                return false;
        //            if (counter > 10 || entity.GetField<string>("laptopWait") != "get")
        //            {
        //                //reset ks icon
        //                if (entity.GetField<string>("customStreak") == streakName)
        //                {
        //                    entity.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
        //                    entity.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
        //                    entity.SetField("customStreak", string.Empty);
        //                }
        //                return false;
        //            }
        //            return true;
        //        });
        //    }
        //    ent.OnNotify("valkaim", entity =>
        //    {
        //        ent.SetField("flag", 0);
        //        string weapon = ent.CurrentWeapon;
        //        if (weapon == "uav_strike_projectile_mp")
        //        {
        //            AfterDelay(400, () =>
        //                initRideKillstreak2(ent));
        //        }
        //    });
        //}
        //private void initRideKillstreak2(Entity entity)
        //{
        //    if (entity.GetField<string>("laptopWait") == "get")
        //        entity.SetField("laptopWait", string.Empty);
        //    else if (entity.GetField<string>("laptopWait") == "weapon_switch_started")
        //    {
        //        ClearUsingRemote(entity);
        //        return;
        //    }
        //    if ((!entity.IsAlive) || (entity.GetField<string>("laptopWait") == "death" && entity.GetField<string>("sessionteam") == "spectator"))
        //    {
        //        ClearUsingRemote(entity);
        //        return;
        //    }
        //    entity.Call("VisionSetNakedForPlayer", "black_bw", 0.75f);

        //    int Count = 0;
        //    entity.SetField("laptopWait", "get");
        //    entity.OnInterval(100, player =>
        //    {
        //        Count++;
        //        if (player == null)
        //            return false;
        //        if (Count > 8 || player.GetField<string>("laptopWait") != "get")
        //        {
        //            clearRideIntro(player, 1.0f);

        //            if (player.GetField<string>("sessionteam") == "spectator")
        //            {
        //                ClearUsingRemote(entity);
        //                return false;
        //            }

        //            FirePredator(player);

        //            return false;
        //        }
        //        return true;
        //    });
        //}
        //private void ClearUsingRemote(Entity ent)
        //{
        //    ent.Call("enableOffhandWeapons");
        //    string curWeapon = ent.CurrentWeapon;
        //    var currentWeapon = ent.Call<string>(33490);
        //    if (ent.Call<int>(33470, "uav_strike_projectile_mp") == 0 && ent.Call<int>(33471, "uav_strike_projectile_mp") == 0)
        //    {
        //        ent.TakeWeapon(curWeapon);
        //        ent.Call("SwitchToWeapon", ent.GetField<string>("lastDroppableWeapon"));
        //    }
        //    else
        //    {
        //        ent.Call("DisableOffhandWeapons");
        //    }
        //    ent.Call("freezeControls", false);
        //    ent.Notify("stopped_using_remote");
        //}
        //private static void clearRideIntro(Entity ent, float delay = 0.0f)
        //{
        //    if (delay >= 0.1)
        //    {
        //        ent.AfterDelay(Convert.ToInt32(delay * 1000), entity =>
        //        {
        //            entity.Call("VisionSetNakedForPlayer", string.Empty, 0);
        //        });
        //    }
        //    else
        //        ent.Call("VisionSetNakedForPlayer", string.Empty, 0);
        //}
        //private void FirePredator(Entity ent)
        //{

        //        Vector3 forward = Call<Vector3>("anglestoforward", ent.Call<Vector3>("getplayerangles"));
        //        Vector3 targetPos = Vector3.RandomXY();
        //            targetPos = ent.Origin + (forward * missileRemoteLaunchTargetDist);

        //        ent.Call("setweaponammoclip", "uav_strike_projectile_mp", 0);

        //        Entity rocket = Call<Entity>("MagicBullet", "stinger_mp", ent.Call<Vector3>("gettagorigin", "tag_weapon_left"), targetPos, ent);

        //    if (rocket == null)
        //    {
        //        ClearUsingRemote(ent);
        //        return;
        //    }

        //    rocket.Call("setCanDamage", true);
        //    MissileEyes(ent, rocket);
        //}

        //private void MissileEyes(Entity player, Entity rocket)
        //{
        //    player.Call("VisionSetMissilecamForPlayer", "black_bw", 0f);

        //    if (rocket == null)
        //        ClearUsingRemote(player);

        //    RidingPred.Add(player);

        //    player.Call("VisionSetMissilecamForPlayer", GetThermalVision(), 1.0f);
        //    player.AfterDelay(150, ent =>
        //    {
        //        ent.Call("ThermalVisionFOFOverlayOn");
        //    });
        //    player.Call("CameraLinkTo", rocket, "tag_origin");
        //    player.Call("ControlsLinkTo", rocket);

        //    if (Call<int>("getdvarint", "camera_thirdPerson") == 1)
        //        setThirdPersonDOF(player, false);

        //    rocket.OnNotify("death", _rocket =>
        //    {
        //        if (RidingPred.Contains(player))
        //            RidingPred.Remove(player);

        //        player.Call("ControlsUnlink");
        //        player.Call("freezeControls", true);

        //        //unfinished
        //        if (!GameEnded)
        //            staticEffect(player, 0.5f);

        //        AfterDelay(500, () =>
        //        {
        //            player.Call("ThermalVisionFOFOverlayOff");
        //            player.Call("CameraUnlink");
        //            if (Call<int>("getdvarint", "camera_thirdPerson") == 1)
        //                setThirdPersonDOF(player, true);

        //            if (player.Call<int>(33470, "uav_strike_projectile_mp") == 0 && player.Call<int>(33471, "uav_strike_projectile_mp") == 0)
        //            {
        //                player.TakeWeapon(player.CurrentWeapon);
        //                player.Call("SwitchToWeapon", player.GetField<string>("lastDroppableWeapon"));
        //                player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
        //                player.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
        //                player.SetField("customStreak", string.Empty);
        //            }

        //            ClearUsingRemote(player);
        //        });
        //    });

        //}
        //private string GetThermalVision()
        //{
        //    string str = Call<string>("getMapCustom", "thermal");
        //    if (str == "invert")
        //        return "thermal_snowlevel_mp";
        //    else
        //        return "thermal_mp";
        //}
        //private bool isKillstreakWeapon(string wep)
        //{
        //    if (string.IsNullOrEmpty(wep))
        //        return false;
        //    wep = wep.ToLower();
        //    if (wep == "none")
        //        return false;
        //    string[] split = wep.Split('_');
        //    bool foundSuffix = false;

        //    if (wep != "destructible_car" && wep != "barrel_mp")
        //    {
        //        foreach (string str in split)
        //        {
        //            if (str == "mp")
        //            {
        //                foundSuffix = true;
        //                break;
        //            }
        //        }

        //        if (!foundSuffix)
        //            wep += "_mp";
        //    }

        //    if (wep.Contains("destructible"))
        //        return false;
        //    if (wep.Contains("killstreak"))
        //        return true;
        //    if (isAirdropMarker(wep))
        //        return true;
        //    if ((wep != "destructible_car" && wep != "barrel_mp") && !string.IsNullOrEmpty(Call<string>("weaponInventoryType", wep))
        //        && Call<string>("weaponInventoryType", wep) == "exclusive")
        //        return true;

        //    //added
        //    if (wep.Contains("remote"))
        //        return true;

        //    return false;
        //}
        //public static bool isAirdropMarker(string weaponName)
        //{
        //    switch (weaponName)
        //    {
        //        case "airdrop_marker_mp":
        //        case "airdrop_mega_marker_mp":
        //        case "airdrop_sentry_marker_mp":
        //        case "airdrop_juggernaut_mp":
        //        case "airdrop_juggernaut_def_mp":
        //            return true;
        //        default:
        //            return false;
        //    }
        //}
        //private static void setThirdPersonDOF(Entity ent, bool Enabled)
        //{
        //    if (Enabled)
        //        ent.Call("setDepthOfField", 0f, 110f, 512f, 4096f, 6f, 1.8f);
        //    else
        //        ent.Call("setDepthOfField", 0f, 0f, 512f, 512f, 4f, 0f);
        //}
        //private static void staticEffect(Entity ent, float duration)
        //{
        //    HudElem staticBG = HudElem.NewClientHudElem(ent);
        //    staticBG.HorzAlign = "fullscreen";
        //    staticBG.VertAlign = "fullscreen";
        //    staticBG.SetShader("white", 640, 480);
        //    staticBG.Archived = true;
        //    staticBG.Sort = 10;

        //    HudElem _static = HudElem.NewClientHudElem(ent);
        //    _static.HorzAlign = "fullscreen";
        //    _static.VertAlign = "fullscreen";
        //    _static.SetShader("ac130_overlay_grain", 640, 480);
        //    _static.Archived = true;
        //    _static.Sort = 20;

        //    ent.AfterDelay(Convert.ToInt32(duration * 1000), entity =>
        //    {
        //        staticBG.Call("destroy");
        //        _static.Call("destroy");
        //    });
        //}
    }
}
                        