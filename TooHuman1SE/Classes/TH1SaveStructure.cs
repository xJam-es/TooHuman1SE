using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using Isolib.IOPackage;
using Isolib.STFSPackage;
using Newtonsoft.Json;
using MessageBox = System.Windows.MessageBox;

namespace TooHuman1SE.SEStructure
{

    /* Error Codes
         -1  : Initial (No Save Loaded)
          0  : OK (No Error)
          1  : No Save Data Was Loaded
          2  : File Size Is Below Minimum
          3  : Data Size value is larger than the actual file-size
          4  : IO Error Caught {exception appended}
          5  : Failed to load Gamesave sectors
          6  : Save Type Not Recognised
          7  : Unable To Write Gamesave
          8  : Unable To Write New Hash
          9  : Failed To Write Character Name To Buffer
         10  : Unable To Write Character Stats To Save Data
         11  : Unable To Write New Filesize
         12  : Unable To Parse Skills Tree
         13  : Unable To Write Skills Tree
         14  : Unable To Parse Runes
         15  : Unable To Write Runes
         16  : Unable To Parse Charms
         17  : Unable To Write Active Charms
         18  : Unable To Write Complete Charms
         19  : Unable To Write Incomplete Charms
         20  : Unable To Parse Weapons
         21  : Unable To Write Weapons
         22  : Unable To Parse Armour
         23  : Unable To Write Armour
    */

    #region Sector
    public class TH1Sector
    {
        private long _id = 0;
        private string _sectorname;
        private byte[] _sectorData;

        public TH1Sector( long inID)
        {
            _id = inID;
            // helpers
            Dictionary<int, string> sectorNamesDic = new TH1Helper().sectorNamesDic;
            if (!sectorNamesDic.TryGetValue((int)id, out _sectorname))
            {
                _sectorname = string.Format("sector{0}", id.ToString().PadLeft(3, '0'));
            }
        }

        public long id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                Dictionary<int, string> sectorNamesDic = new TH1Helper().sectorNamesDic;
                if (!sectorNamesDic.TryGetValue((int)id, out _sectorname))
                {
                    _sectorname = string.Format("sector{0}", id.ToString().PadLeft(3, '0'));
                }
            }
        }
        public long size
        {
            get
            {
                return _sectorData.Length;
            }
        }
        public string sizeString
        {
            get
            {
                string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
                int decimalPlaces = 1;
                long value = _sectorData.Length;

                if (value < 0) { return "Unknown"; }
                if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

                // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
                int mag = (int)Math.Log(value, 1024);

                // 1L << (mag * 10) == 2 ^ (10 * mag) 
                // [i.e. the number of bytes in the unit corresponding to mag]
                decimal adjustedSize = (decimal)value / (1L << (mag * 10));

                // make adjustment when the value is large enough that
                // it would round up to 1000 or more
                if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
                {
                    mag += 1;
                    adjustedSize /= 1024;
                }

                if (mag == 0) decimalPlaces = 0;

                return string.Format("{0:n" + decimalPlaces + "} {1}",
                    adjustedSize,
                    SizeSuffixes[mag]);
            }
        }
        public string name
        {
            get
            {
                return _sectorname;
            }
        }
        public byte[] data
        {
            get
            {
                return _sectorData;
            }
            set
            {
                _sectorData = value;
            }
        }
    }
    #endregion Sector

    #region Helper Classes
    public class TH1Helper
    {
        // Dictionaries
        private Dictionary<char,string> _runeColourNames = new Dictionary<char,string>() {
            { 'G', "Grey" },
            { 'E', "Green" },
            { 'B', "Blue" },
            { 'P', "Purple" },
            { 'O', "Orange" }, // Orange? :P
            { 'R', "Red" }
        };
        private Dictionary<int, string> _runeNames = new Dictionary<int, string>() {
            { 0, "Ansuz" },
            { 1, "Berkano" },
            { 2, "Dagaz" },
            { 3, "Ehwaz" },
            { 4, "Sowilo" },
            { 5, "Fehu" },
            { 6, "Gebo" },
            { 7, "Hagalaz" },
            { 8, "Ingwaz" },
            { 9, "Isa" },
            { 10, "Jera" },
            { 11, "Kenaz" }
        };
        private Dictionary<int, string> _charmTierNames = new Dictionary<int, string>() {
            { 1, "Tier 1" },
            { 2, "Tier 2" },
            { 3, "Tier 3" },
        };
        private Dictionary<char, string> _equipmentColourNames = new Dictionary<char, string>() {
            { 'G', "Grey" },
            { 'E', "Green" },
            { 'B', "Blue" },
            { 'P', "Purple" },
            { 'O', "Orange" },
            { 'R', "Red" }
        };
        private Dictionary<int, string> _weaponTypes = new Dictionary<int, string>()
        {
            {0 , "Dual Swords"},
            { 1 , "1-Handed Sword"},
            { 2 , "2-Handed Sword"},
            { 3 , "Dual Staves"},
            { 4 , "1-Handed Staff"},
            { 5 , "2-Handed Staff"},
            { 6 , "Dual Hammers"},
            { 7 , "1-Handed Hammer"},
            { 8 , "2-Handed Hammer"},
            { 9 , "Laser Pistols"},
            { 10 , "Plasma Pistols"},
            { 11 , "Slug Pistols"},
            { 12 , "Heavy Laser"},
            { 13 , "Heavy Plasma"},
            { 14 , "Heavy Slug"},
            { 15 , "Assault Laser"},
            { 16 , "Assault Plasma"},
            { 17 , "Assault Slug"}
        };
        private Dictionary<int, double> _weaponBaseDamage = new Dictionary<int, double>()
        {
            {0 , 90},
            { 1 , 100},
            { 2 , 110},
            { 3 , 90},
            { 4 , 100},
            { 5 , 110},
            { 6 , 100},
            { 7 , 105},
            { 8 , 110},
            { 9 , 11.5},
            { 10 , 10.8},
            { 11 , 12},
            { 12 , 18},
            { 13 , 14.5},
            { 14 , 17.4},
            { 15 , 15.8},
            { 16 , 13.5},
            { 17 , 15}
        };
        private Dictionary<int, string> _armourTypes = new Dictionary<int, string>()
        {
            {0 , "Head"},
            { 1 , "Torso"},
            { 2 , "Shoulders"},
            { 3 , "Gauntlets"},
            { 4 , "Leggings"},
            { 5 , "Boots"}
        };
        private Dictionary<int, string> _sectorNames = new Dictionary<int, string>()
        {
            {0, "header" },
            {1, "character" },
            {2, "skill_tree" },
            {3, "location" },
            {4, "charms-active" },
            {5, "runes" },
            {6, "charms-available" },
            {7, "weapons" },
            {8, "armour" },
            {11, "charms-incomplete" },
            {12, "weapon-blueprints" },
            {13, "armour-blueprints" }
        };
        private Dictionary<int, string> _classNames = new Dictionary<int, string>()
        {
            {0, "Berserker" },
            {1, "Champion" },
            {2, "Defender" },
            {3, "Heavy Gunner" },
            {4, "Gunslinger" },
            {5, "Commando" },
            {6, "Bio-Engineer" },
            {7, "Dragon" },
            {8, "Rune Master" }
        };
        private Dictionary<int, string> _alignmentNames = new Dictionary<int, string>()
        {
            {0, "None" },
            {1, "Human" },
            {2, "Cybernetics" }
        };
        private Dictionary<int, int> _weaponGroup = new Dictionary<int, int>()
        {
            {0, 0},
            {1, 0},
            {2, 0},
            {3, 0},
            {4, 0},
            {5, 0},
            {6, 0},
            {7, 0},
            {8, 0},
            {9, 1},
            {10, 1},
            {11, 1},
            {12, 1},
            {13, 1},
            {14, 1},
            {15, 1},
            {16, 1},
            {17, 1}
        };

        // Limits
        public int LIMIT_MAX_RUNES = 60;
        public int LIMIT_MAX_CHARMS = 20;

        // Rune Types
        public int RUNE_TYPE_ERROR = -1;
        public int RUNE_TYPE_M = 0;
        public int RUNE_TYPE_U = 1;
        
        // Lookups
        public string getRuneColourName( char _colourID )
        {
            if( !_runeColourNames.TryGetValue(_colourID, out string tmpRes) ) tmpRes = "Unknown";
            return tmpRes;
        }
        public char getRuneColourID( string name)
        {
            char tmpRes = 'G';
            foreach( KeyValuePair<char,string> _kvp in _runeColourNames) if (_kvp.Value == name) tmpRes = _kvp.Key;
            return tmpRes;
        }
        // Colour Names
        public string[] runeColourNameArray
        {
            get
            {
                return _runeColourNames.Values.ToArray();
            }
        }
        public string[] charmTierNameArray
        {
            get
            {
                return _charmTierNames.Values.ToArray();
            }
        }
        public string getRuneName(int _runeID)
        {
            if (!_runeNames.TryGetValue(_runeID, out string tmpRes)) tmpRes = "Unknown";
            return tmpRes;
        }
        // Class Names
        public Dictionary<int, string> classNamesDic
        {
            get
            {
                return _classNames;
            }
        }
        public string[] classNamesArray
        {
            get
            {
                return _classNames.Values.ToArray();
            }
        }
        // Alignment Names
        public string[] alignmentNamesArray
        {
            get
            {
                return _alignmentNames.Values.ToArray();
            }
        }
        public string[] equipmentColourNamesArray
        {
            get
            {
                return _equipmentColourNames.Values.ToArray();
            }
        }
        public string[] weaponTypesArray
        {
            get
            {
                return _weaponTypes.Values.ToArray();
            }
        }
        public string[] armourTypesArray
        {
            get
            {
                return _armourTypes.Values.ToArray();
            }
        }
        public Dictionary<int,string> sectorNamesDic
        {
            get
            {
                return _sectorNames;
            }
        }
        public Dictionary<int, string> weaponTypesDic
        {
            get
            {
                return _weaponTypes;
            }
        }
        public Dictionary<int, string> armourTypesDic
        {
            get
            {
                return _armourTypes;
            }
        }
        public Dictionary<char, string> equipmentColourNames
        {
            get
            {
                return _equipmentColourNames;
            }
        }
        public int[] weaponWriteOrder
        {
            get
            {
                return new int[] {
                    4, 5, 3, 1,
                    2, 0, 7, 8,
                    6, 9, 11, 10,
                    12, 13, 14, 15,
                    17, 16
                };
            }
        }
        public int[] armourWriteOrder
        {
            get
            {
                return new int[] {
                    5, 4, 0,
                    3, 2, 1
                };
            }
        }
        // Helpers
        public int getWeaponGroup( int weaponType )
        {
            // 0 = Melee
            // 1 = Ranged
            if (!_weaponGroup.TryGetValue(weaponType, out int weaponGroup)) weaponGroup = -1;
            return weaponGroup;
        }

        public int getArmourGroup(int armourType)
        {
            // 0 = All For Now
            return 0;
        }
    }
    public class TH1Collections
    {
        // Private
        private TH1PaintCollection _paintCollection;
        private TH1MutationCollection _mutationCollection;
        private TH1CharmQuestCollection _questCollection;
        private TH1CharmQuestTypeCollection _questTypeCollection;
        private TH1RuneMBonusCollection _runeMBonusCollection;
        private TH1ArmourBaseStatsCollection _armourBaseStatsCollection;
        private TH1RuneMCollection _runeMCollection;
        private TH1RuneUCollection _runeUCollection;
        private TH1CharmCollection _charmCollection;
        private TH1WeaponCollection _weaponCollection;
        private TH1ArmourCollection _armourCollection;
        // + Helper
        private TH1Helper _helper;

        // Construction
        public TH1Collections()
        {
            // Helper Collections
            if (_paintCollection == null) _paintCollection = new TH1PaintCollection();
            if (_mutationCollection == null) _mutationCollection = new TH1MutationCollection();
            if (_questTypeCollection == null) _questTypeCollection = new TH1CharmQuestTypeCollection();
            if (_questCollection == null) _questCollection = new TH1CharmQuestCollection(this.questTypeCollection);
            if (_runeMBonusCollection == null) _runeMBonusCollection = new TH1RuneMBonusCollection();
            if (_armourBaseStatsCollection == null) _armourBaseStatsCollection = new TH1ArmourBaseStatsCollection();

            // Databases
            if (_runeMCollection == null) _runeMCollection = new TH1RuneMCollection(runeMBonusCollection);
            if (_runeUCollection == null) _runeUCollection = new TH1RuneUCollection(this.mutationCollection);
            if (_charmCollection == null) _charmCollection = new TH1CharmCollection(this.questCollection, this.mutationCollection);
            if (_weaponCollection == null) _weaponCollection = new TH1WeaponCollection(this.paintCollection);
            if (_armourCollection == null) _armourCollection = new TH1ArmourCollection(this.paintCollection, this.armourBaseStatsCollection);

            // Helper
            if (_helper == null) _helper = new TH1Helper();
        }

        // Public
        public TH1PaintCollection paintCollection
        {
            get
            {
                return _paintCollection;
            }
        }
        public TH1MutationCollection mutationCollection
        {
            get
            {
                return _mutationCollection;
            }
        }
        public TH1CharmQuestCollection questCollection
        {
            get
            {
                return _questCollection;
            }
        }
        public TH1CharmQuestTypeCollection questTypeCollection
        {
            get
            {
                return _questTypeCollection;
            }
        }
        public TH1RuneMBonusCollection runeMBonusCollection
        {
            get
            {
                return _runeMBonusCollection;
            }
        }
        public TH1ArmourBaseStatsCollection armourBaseStatsCollection
        {
            get
            {
                return _armourBaseStatsCollection;
            }
        }
        public TH1RuneMCollection runeMCollection
        {
            get
            {
                return _runeMCollection;
            }
        }
        public TH1RuneUCollection runeUCollection
        {
            get
            {
                return _runeUCollection;
            }
        }
        public TH1CharmCollection charmCollection
        {
            get
            {
                return _charmCollection;
            }
        }
        public TH1WeaponCollection weaponCollection
        {
            get
            {
                return _weaponCollection;
            }
        }
        public TH1ArmourCollection armourCollection
        {
            get
            {
                return _armourCollection;
            }
        }
        public TH1Helper helper
        {
            get
            {
                return _helper;
            }
        }
    }
    #endregion Helper Classes

    #region TH1Character
    public class TH1Character
    {
        // Offsets
        public long OFFSET_HEADER = 0;
        public long OFFSET_SAVESLOT = 4;
        public long OFFSET_UNKNOWN_01 = 8;
        public long OFFSET_LAST_SAVED = 12;
        public long OFFSET_NAME_U = 20;
        public long OFFSET_CLASS = 52;
        public long OFFSET_LEVELA = 56;
        public long OFFSET_ENEMIES_KILLED = 124;
        public long OFFSET_SKILLPOINTSA = 128;
        public long OFFSET_DATA_PAIRSA = 956;
        public long OFFSET_DATA_PAIRSB = 1652;
        public long OFFSET_LEVELB = 1748;
        public long OFFSET_CURR_LEVEL_EXP = 1752;
        public long OFFSET_EXP = 1756;
        public long OFFSET_SKILLPOINTSB = 1760;
        public long OFFSET_BOUNTY = 1764;
        public long OFFSET_NAME_A_LENGTH = 1800;
        public long OFFSET_NAME_A = 1804;

        // Limits
        public int LIMIT_NAME_LENGTH = 15;
        public int LIMIT_DATA_PAIRSA = 87;
        public int LIMIT_DATA_PARISB = 7;

        // Variables
        public string name;
        public long alignment;
        public long charClass;
        public long level;
        public long exp;
        public long bounty;
        public long skillPoints;
        public string playTime;
        public long saveSlot;
        public DateTime lastSave;

        public long OFFSET_ALIGNMENT = 0; // After Ascii Name (uint 14/14)

        // Data Pairs
        public Dictionary<string, uint> dataPairsA = new Dictionary<string, uint>();
        public Dictionary<string, float> dataPairsB = new Dictionary<string, float>();

        // Data Pair Names
        public Dictionary<uint, string> dataPairNamesA = new Dictionary<uint, string> {
            [0x01] = "goblins_killed",
            [0x02] = "trolls_killed",
            [0x03] = "dark_elves_killed",
            [0x04] = "undead_killed",
            [0x10] = "air_kills",
            [0x11] = "ruiner_kills",
            [0x2D] = "wells_activated",
            [0x2E] = "rifle_shots_fired", // Primary or Secondary
            [0x52] = "rune_pickups",
            [0x5B] = "hall_of_heros_01",
            [0x5C] = "hall_of_heros_02",
            [0x5D] = "hall_of_heros_03",
            [0x5E] = "hall_of_heros_spare",
            [0x5F] = "ice_forest_01",
            [0x60] = "ice_forest_02",
            [0x61] = "ice_forest_03",
            [0x62] = "ice_forest_04",
            [0x63] = "world_serpent_01",
            [0x64] = "world_serpent_02",
            [0x65] = "world_serpent_03",
            [0x66] = "world_serpent_04",
            [0x67] = "world_serpent_05",
            [0x68] = "helheim_01",
            [0x69] = "helheim_02",
            [0x6A] = "helheim_03",
            [0x6B] = "helheim_04",
            [0x6C] = "helheim_05",
            [0x7A] = "highest_combo",
            [0x7B] = "item_pickups",
            [0x7C] = "items_crafted"
        };
        public Dictionary<uint, string> dataPairNamesB = new Dictionary<uint, string>
        {
            [0x02] = "playtime_seconds",
            [0x03] = "playtime_minutes",
            [0x04] = "playtime_hours"
        };

        
    }
    #endregion TH1Character

    #region TH1SkillsTree
    public class TH1SkillsTreePair
    {
        public long first = 0;
        public long second = 0;
    }

    public class TH1SkillsTree
    {
        // Constants
        private long ST_VALUES_DEFAULT = 65;
        private long ST_VALUES_CHAMPION = 67;

        // Limits
        public long LIMIT_SKILL_MAX = 99999;

        // Variables
        private long cClass = 0;

        // Skill Names
        private string[] NAMES_0 = new string[] { // Berserker
            "Spiritual Runier",
            "A Capacity for Rage",
            "The Bear's Bolling Blood",
            "Onslaught of Claws",
            "Brutality",
            "Loki's Kiss",
            "Ankle Biter",
            "Sleep-Storm of Steel",
            "Swift of Claw",
            "Engulfing Rage",
            "Shield Biter",
            "Unrelenting Blades",
            "Weapon Recovery",
            "Warrior of the Twinned-Claw",
            "Spirit of Fenrir"
        };
        private string[] NAMES_1 = new string[] { // Champion
            "Raven Call",
            "Unerring Strike",
            "Immolating Blade",
            "Asgard's Fury",
            "Kinship of Gungnir",
            "Lament for the Battle-Slain",
            "Thermal Induction Mine",
            "Feeder of Ravens",
            "Tree of Raining-Iron",
            "One Will Rise Above",
            "Valiant's Might",
            "Storm of Mortal Wounds",
            "Warrior of the Blood-Eel",
            "Ascent to Valhalla",
            "Stopping Power",
            "Spirit of Fenrir"
        };
        private string[] NAMES_2 = new string[] { // Defender
            "Valiant's Unstable Hand",
            "Defender’s Resilience",
            "Enthalpy Reduction Attack",
            "Grim Resolve",
            "The Berserker's Grief",
            "Enthalpy Reduction Mines",
            "Ward of the NORNs",
            "Tree of Scorching Light",
            "Fimbulwinter's Numbing Touch",
            "Reversal of Wyrds",
            "Egil's Blessing",
            "Adept of the Light-Spear",
            "Tyr's Best Work",
            "Warrior of the Iron Fist",
            "Spirit of Fenrir "
        };
        private string[] NAMES_5 = new string[] { // Commando
            "Wrecker of Mead Halls",
            "Pinning Shot",
            "Rain of Iron",
            "Adept of the Burning Spear",
            "Bullet-Tree",
            "Cluster Munitions",
            "Tree of Shrieking-Flame",
            "Smoothbore",
            "Lightning Cascade",
            "Cut to the Bone",
            "Ballistic Telemetry Feedback",
            "Delayed Fragmentation Warheads",
            "Gift of Gungnir",
            "Spirit of Fenrir",
            "Helm Reddener"
        };
        private string[] NAMES_6 = new string[] { // Bio-Engineer
            "Valkyrie's Blessing",
            "Idunn's Touch",
            "Skuld's Embrace",
            "Warrior of the Battle-Oar",
            "Warrior of Tyr's Way",
            "Wrack of Lightning Mine",
            "Ward of the NORNs",
            "Gifts of Idunn",
            "Idunn's Boon",
            "Idunn's Favor",
            "Idunn's Wish",
            "Ascent to Valhalla",
            "Cellular Rebonding",
            "Electrified Blade",
            "Spirit of Fenrir"
        };

        // Skills Tree Values
        public List<TH1SkillsTreePair> pairs = new List<TH1SkillsTreePair>();

        public TH1SkillsTree( long setClass )
        {
            cClass = setClass;
        }

        public string[] getSkillNames()
        {
            string[] tmpout = new string[15];
            switch (cClass)
            {
                case 0: tmpout = NAMES_0; break;
                case 1: tmpout = NAMES_1; break;
                case 2: tmpout = NAMES_2; break;
                case 5: tmpout = NAMES_5; break;
                case 6: tmpout = NAMES_6; break;
                default: for (int n = 0; n < tmpout.Length; n++) tmpout[n] = "Unknown"; break;
            }
            return tmpout;
        }

        public long getValueCount()
        {
            if (cClass == 1) return ST_VALUES_CHAMPION;
            else return ST_VALUES_DEFAULT;
        }
    }
    #endregion TH1SkillsTree

    class TH1ExpToNextLevel
    {
        public long[] expUp = {
            450, 690, 970, 1290, 1650,
            2050, 2710, 3670, 4710, 5805,
            7515, 9339, 11277, 13329, 15495,
            17775, 20169, 22677, 25299, 24000,
            28000, 30000, 32000, 33000, 35000,
            37000, 39000, 41000, 42000, 43000,
            45000, 47000, 49000, 51000, 53000,
            55000, 57000, 59000, 61000, 64000,
            67000, 70000, 73000, 76000, 79000,
            82000, 85000, 88000, 91000
        };
        public long[] baseEXP;

        public TH1ExpToNextLevel()
        {
            baseEXP = new long[expUp.Length+1];
            for( int baseoffset = 1; baseoffset < expUp.Length+1; baseoffset++)
            {
                baseEXP[baseoffset] = baseEXP[baseoffset - 1] + expUp[baseoffset - 1];
            }
        }

        public int calcLevel( long exp)
        {
            int tmpres = 0;
            while ((tmpres < baseEXP.Length) && (exp >= baseEXP[tmpres])) tmpres++;
            return tmpres;
        }

        public long expToNext(long exp)
        {
            long tmpres = 0;
            if (exp > 0)
            {
                int lvl = calcLevel(exp);
                if (lvl < baseEXP.Length) tmpres = expUp[lvl - 1];
            }
            return tmpres;
        }

        public long expProgressToNext(long exp)
        {
            long tmpres = 0;
            int lvl = calcLevel(exp);
            long toNext = expToNext(exp);
            if (toNext > 0) tmpres = expUp[lvl-1]-(baseEXP[lvl]-exp);
            return tmpres;
        }

    }

    #region TH1Paint

    public class TH1Paint
    {
        // Private
        private int _paintID;
        private string _paintName;
        private string _paintUse;

        // Public
        public int paintID
        {
            get
            {
                return _paintID;
            }
        }
        public string paintName
        {
            get
            {
                return _paintName;
            }
        }
        public string paintUse
        {
            get
            {
                return _paintUse;
            }
        }

        // Construction
        public TH1Paint( int id, string name, string use)
        {
            _paintID = id;
            _paintName = name;
            _paintUse = use;
        }
        public TH1Paint()
        {
            _paintID = 1;
            _paintName = "default";
            _paintUse = "";
        } // Default (valid paint)
    }
    public class TH1PaintJson
    {
        [JsonProperty("ID")] public int ID { get; set; }
        [JsonProperty("Name")] public string Name { get; set; }
        [JsonProperty("Use")] public string Use { get; set; }
    }
    public class TH1PaintCollection
    {
        // The Collection
        private Dictionary<int, TH1Paint> _collection = new Dictionary<int, TH1Paint>();

        public TH1PaintCollection()
        {
            // Load The Library
            string json;
            Dictionary<int, TH1PaintJson> _parseCollection = new Dictionary<int, TH1PaintJson>();

            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/Paint.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<int, TH1PaintJson>>(json);
            foreach (KeyValuePair<int, TH1PaintJson> _item in _parseCollection)
            {
                TH1PaintJson _paint = _item.Value;
                TH1Paint _tmpPaint = new TH1Paint(_item.Key,_paint.Name,_paint.Use);
                _collection.Add(_item.Key, _tmpPaint);
            }
        }

        // Return Paint
        public TH1Paint findPaint( int id)
        {
            if (!_collection.TryGetValue(id, out TH1Paint tmpRes)) tmpRes = new TH1Paint();
            return tmpRes;
        }
        public TH1Paint findPaint(string name)
        {
            foreach( KeyValuePair<int,TH1Paint> _kvp in _collection) if (_kvp.Value.paintName == name) return _kvp.Value;
            return new TH1Paint();
        }

        // Other Functions
        public string[] paintNameArray()
        {
            List<TH1Paint> values = Enumerable.ToList(_collection.Values);
            string[] tmpRes = new string[values.Count];
            for (int i = 0; i < tmpRes.Length; i++) tmpRes[i] = values[i].paintName;
            return tmpRes;
        }
    }

    #endregion TH1Paint

    #region Generic Rune Functions
    public class TH1RuneSummary
    {
        // rune Holders
        TH1RuneM _runeM = null;
        TH1RuneU _runeU = null;

        // Summary Fields
        private string _runeName;
        private string _runeType;
        private string _runeEffect;
        private int _runeLevel;

        public TH1RuneSummary()
        {
            // Creator (meh)
        }

        // The Setup
        public void setRune(object rune)
        {
            loadRune(rune);
        }
        public void setRune(TH1Collections db, string runeString)
        {
            // Check & Load
            if(runeString.Contains("_U_")) { 
                _runeU = db.runeUCollection.findRune(runeString);
                loadRune(_runeU);
                return;
            }
            if (runeString.Contains("_M_"))
            {
                _runeM = db.runeMCollection.findRune(runeString);
                loadRune(_runeM);
                return;
            }
        }
        public void setRune(TH1Collections db, byte[] nullString)
        {
            char[] asciiChars = new char[Encoding.ASCII.GetCharCount(nullString, 0, nullString.Length - 1)];
            Encoding.ASCII.GetChars(nullString, 0, nullString.Length - 1, asciiChars, 0); // Remove Null
            string tmpString = new string(asciiChars); ;
            setRune(db, tmpString);
        }

        // Summary Builder
        private void loadRune( object rune)
        {
            _runeM = rune as TH1RuneM;
            _runeU = rune as TH1RuneU;

            if (_runeM != null)
            {
                _runeName = _runeM.bonusName;
                _runeType = "Bonus";
                _runeEffect = _runeM.bonusType;
                _runeLevel = _runeM.runeLevel;
            }

            if (_runeU != null)
            {
                _runeName = _runeU.runeName;
                _runeType = "Mutator";
                _runeEffect = _runeU.mutationName;
                _runeLevel = _runeU.runeLevel;
            }
        }

        // Output
        public string Name
        {
            get
            {
                return _runeName;
            }
        }
        public string Type
        {
            get
            {
                return _runeType;
            }
        }
        public string Effect
        {
            get
            {
                return _runeEffect;
            }
        }
        public int Level
        {
            get
            {
                return _runeLevel;
            }
        }
        public int runeType
        {
            get
            {
                if (_runeM != null) return 0;
                if (_runeU != null) return 1;
                return -1;
            }
        }
        public object rune
        {
            get
            {
                if (_runeM != null) return _runeM;
                else return _runeU;
            }
        }
    }
    #endregion Generic Rune Functions

    #region TH1RuneM

    public class TH1RuneM
    {
        // Private
        int _runeID;
        char _runeColourKey;
        int _runeLevel;
        TH1RuneMBonus _runeBonus;
        int _runeBonusValue;
        int _runeType;
        int _runeValue;
        int _runeCraftCost;
        double _runeBaseStatFactor; // Base Stats Ahoy!

        // Public
        public int runeID
        {
            get
            {
                return _runeID;
            }
        }
        public char runeColourKey
        {
            get
            {
                return _runeColourKey;
            }
        }
        public int runeLevel
        {
            get
            {
                return _runeLevel;
            }
        }
        public TH1RuneMBonus runeBonus
        {
            get
            {
                return _runeBonus;
            }
        }
        public int runeBonusValue
        {
            get
            {
                return _runeBonusValue;
            }
        } // to be updated?
        public int runeType
        {
            get
            {
                return _runeType;
            }
        }
        public int runeValue
        {
            get
            {
                return _runeValue;
            }
        }
        public int runeCraftCost
        {
            get
            {
                return _runeCraftCost;
            }
        }
        public double runeBaseStatFactor
        {
            get
            {
                return _runeBaseStatFactor;
            }
        }

        // Construction
        public TH1RuneM(int ID, char colourKey, int level, TH1RuneMBonus bonus, int bonusValue, int type, int value, int craftCost, double baseStatFactor)
        {
            _runeID = ID;
            _runeColourKey = colourKey;
            _runeLevel = level;
            _runeBonus = bonus;
            _runeBonusValue = bonusValue;
            _runeType = type;
            _runeValue = value;
            _runeCraftCost = craftCost;
            _runeBaseStatFactor = baseStatFactor;
        }
        public TH1RuneM()
        {
            _runeID = 1;
            _runeColourKey = 'G';
            _runeLevel = 1;
            _runeBonus = new TH1RuneMBonus();
            _runeBonusValue = 1;
            _runeType = 0;
            _runeValue = 100;
            _runeCraftCost = 100;
            _runeBaseStatFactor = 0.2;
        } // overload for default (valid rune)

        // Additional Public
        public string runeString
        {
            get
            {
                return runeColourKey + "_M_" + runeID.ToString();
            }
        }
        public byte[] runeNullArray
        {
            get
            {
                string tmpString = runeString;
                byte[] tmpRes = new byte[tmpString.Length + 1];
                for (int i = 0; i < tmpString.Length; i++)
                {
                    tmpRes[i] = Convert.ToByte(tmpString[i]);
                }
                return tmpRes;
            }
        } // Null Terminated Array
        public string longName
        {
            get
            {
                string suffix_type = "";
                string suffix_value = "";
                if (runeBonus.bonusName != runeBonus.bonusType) suffix_type = string.Format(" ({0})",runeBonus.bonusType);
                if (runeBonus.bonusName != "Colour Module") suffix_value = string.Format(" +{0}%", runeBonusValue);
                return string.Format("L{0} {1}{2}{3}", runeLevel, runeBonus.bonusName, suffix_type, suffix_value);
                // return string.Format("L{4} {0}: {1} +{2}% [{3}]", runeColourName, runeBonus.bonusName, runeBonusValue, runeID, runeLevel);
            }
        }

        public string bonusName
        {
            get
            {
                return runeBonus.bonusName;
            }
        }
        public string bonusNameAndValue
        {
            get
            {
                if(bonusName == "Colour Module") return string.Format("{0}", bonusName);
                else return string.Format("{0} +{1}%", bonusName, runeBonusValue);
            }
        }
        public string bonusType
        {
            get
            {
                string suffix = "";
                if (bonusName != "Colour Module") suffix = string.Format(" +{0}%", runeBonusValue);          
                return string.Format("{0}{1}", runeBonus.bonusType, suffix);
            }
        }
        public string LevelAndBonusName
        {
            get
            {
                if (bonusName == "Colour Module") return string.Format("{1}", runeID, bonusName);
                else return string.Format("{1} {2}", runeID, runeLevel, bonusNameAndValue);
            }
        }
        public string LevelAndBonusType
        {
            get
            {
                if (bonusName == "Colour Module") return string.Format("{1}", runeID, bonusName);
                else return string.Format("L{1} {2}", runeID, runeLevel, bonusType);
            }
        }
        public string idLevelAndBonusName
        {
            get
            {
                return string.Format("{0}: {1}", runeID, LevelAndBonusName);
            }
        }
        public string idLevelAndBonusType
        {
            get
            {
                return string.Format("{0}: {1}", runeID, LevelAndBonusType);
            }
        }
        public string runeColourName
        {
            get
            {
                TH1Helper _help = new TH1Helper();
                return _help.getRuneColourName(_runeColourKey);
            }
        }
        public string runeTypeName
        {
            get
            {
                TH1Helper _help = new TH1Helper();
                return _help.getRuneName(runeType);
            }
        }
        public BitmapImage runeImage
        {
            get
            {
                BitmapImage tmpres;
                try
                {
                    tmpres = new BitmapImage(new Uri(string.Format("pack://application:,,,/Icons/Runes/{0}{1}.png", runeColourKey, runeType)));
                }
                catch { tmpres = new BitmapImage(new Uri("pack://application:,,,/Icons/Runes/unknown.png"));  }
                return tmpres;
            }
        }
    } // A Single Rune
    public class TH1RuneMJson
    {
        [JsonProperty("ID")] public int ID { get; set; }
        [JsonProperty("ColourKey")] public char ColourKey { get; set; }
        [JsonProperty("Level")] public int Level { get; set; }
        [JsonProperty("BonusID")] public string BonusID { get; set; }
        [JsonProperty("BonusValue")] public int BonusValue { get; set; }
        [JsonProperty("RuneType")] public int RuneType { get; set; }
        [JsonProperty("RuneValue")] public int RuneValue { get; set; }
        [JsonProperty("CraftCost")] public int CraftCost { get; set; }
        [JsonProperty("BaseStatFactor")] public double BaseStatFactor { get; set; }
    } // JSON Structure
    public class TH1RuneMCollection // A Collection Of Runes (Orange? :P)
    {
        // The Collection
        private Dictionary<string, TH1RuneM> _collection = new Dictionary<string, TH1RuneM>();
        private Dictionary<char, int> _countByColour = new Dictionary<char, int>();

        // Create
        public TH1RuneMCollection( TH1RuneMBonusCollection bonusCollection)
        {
            // Load The Library
            string json;
            Dictionary<string, TH1RuneMJson> _parseCollection = new Dictionary<string, TH1RuneMJson>();

            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/RuneMCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            } catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString());  }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<string, TH1RuneMJson>>(json);
            foreach( KeyValuePair<string,TH1RuneMJson> _item in _parseCollection)
            {
                TH1RuneMJson _rune = _item.Value;
                TH1RuneMBonus _bonus = bonusCollection.findBonus(_rune.BonusID);
                TH1RuneM _tmpRune = new TH1RuneM(_rune.ID, _rune.ColourKey, _rune.Level, _bonus, _rune.BonusValue, _rune.RuneType, _rune.RuneValue, _rune.CraftCost, _rune.BaseStatFactor);
                _collection.Add(_item.Key, _tmpRune);

                if(_countByColour.TryGetValue(_rune.ColourKey, out int _colourCount)) _countByColour[_rune.ColourKey] += 1;
                else _countByColour.Add(_rune.ColourKey, _rune.ID);
            }

        }

        // Return Rune
        public TH1RuneM findRune(char colourCode, int runeID)
        {
            return findRune(colourCode + "_M_" + runeID.ToString());
        }
        public TH1RuneM findRune(byte[] nullString)
        {
            char[] asciiChars = new char[Encoding.ASCII.GetCharCount(nullString, 0, nullString.Length-1)];
            Encoding.ASCII.GetChars(nullString, 0, nullString.Length-1, asciiChars, 0); // Remove Null
            string tmpString = new string(asciiChars); ;
            return findRune(tmpString);
        }
        public TH1RuneM findRune( string runeString)
        {
            _collection.TryGetValue(runeString, out TH1RuneM tmpRes);
            return tmpRes;
        }
        public TH1RuneM findRuneByBonusLongNameID( string _bonusLNI)
        {
            TH1RuneM _tmpres = new TH1RuneM();
            foreach( KeyValuePair<string,TH1RuneM> _kvp in _collection)
            {
                if (_kvp.Value.runeBonus.bonusNameID == _bonusLNI) return _kvp.Value;
            }
            return _tmpres;
        }

        // Check Rune
        public bool runeValid(char colourCode, int runeID)
        {
            return runeValid(colourCode + "_M_" + runeID.ToString());
        }
        public bool runeValid ( string runeString)
        {
            return _collection.TryGetValue(runeString, out TH1RuneM _tmp);
        }

        // Return Rune
        public TH1RuneM randomRune()
        {
            Random rand = new Random();
            List<TH1RuneM> values = Enumerable.ToList(_collection.Values);
            int size = _collection.Count;
            return values[rand.Next(size)];
        }
        public TH1RuneM randomRuneByColour(char _colourKey)
        {
            Random rand = new Random();
            List<TH1RuneM> values = new List<TH1RuneM>();
            foreach( KeyValuePair<string,TH1RuneM> _kvp in _collection) if (_kvp.Value.runeColourKey == _colourKey) values.Add(_kvp.Value);
            int size = values.Count;
            return values[rand.Next(size)];
        }

        // Other Functions
        public string[] idLevelAndBonusArray( char _colour)
        {
            List<string> tmpRes = new List<string>();
            foreach( KeyValuePair<string,TH1RuneM> _kvp in _collection)
            {
                if (_kvp.Key[0] == _colour) tmpRes.Add(_kvp.Value.idLevelAndBonusType);
            }
            return tmpRes.ToArray();
        }
    }
    public class TH1RuneMExt
    {
        // Private
        private TH1RuneM _rune;

        // Public
        public TH1RuneM rune
        {
            get
            {
                return _rune;
            }
        }
        public uint purchased { get; set; }
        public uint valueModifier { get; set; }
        public uint dataB { get; set; }
        public uint dataD { get; set; }
        public TH1Paint paint { get; set; }

        // Functions
        public TH1RuneMExt( TH1RuneM runeM )
        {
            _rune = runeM;
        }
        public byte[] runeExtToArray { 
            get
            {
                string tmpName = rune.runeString;
                byte[] tmpRune = new byte[tmpName.Length+1+(4*6)];
                RWStream writer = new RWStream(tmpRune, true, true);
                try
                {
                    // Rune String
                    writer.WriteUInt32((uint)tmpName.Length+1);
                    writer.WriteString(tmpName, StringType.Ascii, tmpName.Length);
                    writer.WriteBytes( new byte[] { 0x00 });

                    // Rune Ext
                    writer.WriteUInt32(purchased);
                    writer.WriteUInt32(dataB);
                    writer.WriteUInt32(valueModifier);
                    writer.WriteUInt32(dataD);
                    writer.WriteUInt32((uint)paint.paintID);
                }
                catch { }
                finally { writer.Flush(); tmpRune = writer.ReadAllBytes(); writer.Close(true); }
                return tmpRune;
            }
        }
        public int calcValue
        {
            get {
                try
                {
                    // dial m for deciMal
                    decimal tmpVal = (rune.runeValue * (1m/4m)) * (valueModifier / 10000m);
                    return Convert.ToInt32(tmpVal);
                } catch { return 0;  }
            }
        }

    } // Rune + Gamesave Data
    public class TH1RuneMBonus
    {
        // Private
        private string _bonusID;
        private string _bonusName;
        private string _bonusTypeA;
        private string _bonusTypeB;
        private int _bonusMax;

        // Public
        public string bonusID
        {
            get
            {
                return _bonusID;
            }
        }
        public string bonusName
        {
            get
            {
                return _bonusName;
            }
        }
        public string bonusTypeA
        {
            get
            {
                return _bonusTypeA;
            }
        }
        public string bonusTypeB
        {
            get
            {
                return _bonusTypeB;
            }
        }
        public string bonusType
        {
            get
            {
                if (bonusTypeB != "") return bonusTypeB;
                else if (bonusTypeA != "") return bonusTypeA;
                else return bonusName;
            }
        }
        public int bonusMax
        {
            get
            {
                return _bonusMax;
            }
        }
        public string bonusLongName
        {
            get
            {
                string tmpBonus = "";
                if (bonusTypeB != "") tmpBonus = bonusTypeB;
                else if (bonusTypeA != "") tmpBonus = bonusTypeA;
                else tmpBonus = bonusName;
                return string.Format("{0} (Max {1}%)", tmpBonus, bonusMax);
            }
        }
        public string bonusNameID
        {
            get
            {
                string tmpBonus = "";
                if (bonusTypeB != "") tmpBonus = bonusTypeB;
                else if (bonusTypeA != "") tmpBonus = bonusTypeA;
                else tmpBonus = bonusName;
                return string.Format("{0}: {1}",bonusID, tmpBonus);
            }
        }

        // Construction
        public TH1RuneMBonus( string id, string name, string typeA, string typeB, int maxBonus ) {
            _bonusID = id;
            _bonusName = name;
            _bonusTypeA = typeA;
            _bonusTypeB = typeB;
            _bonusMax = maxBonus;
        }
        public TH1RuneMBonus()
        {
            _bonusID = "G_0";
            _bonusName = "Balance";
            _bonusTypeA = "Air Melee Damage";
            _bonusTypeB = "";
            _bonusMax = 30;
        } // Default (valid bonus)
    }
    public class TH1RuneMBonusJson
    {
        [JsonProperty("Bonus")] public string ID { get; set; }
        [JsonProperty("Name")] public string name { get; set; }
        [JsonProperty("Type")] public string typeA { get; set; }
        [JsonProperty("Type2")] public string typeB { get; set; }
        [JsonProperty("Max Bonus")] public int maxBonus { get; set; }
    }
    public class TH1RuneMBonusCollection
    {
        Dictionary<string, TH1RuneMBonus> _collection = new Dictionary<string, TH1RuneMBonus>();

        public TH1RuneMBonusCollection()
        {
            string json;
            Dictionary<string, TH1RuneMBonusJson> _parseCollection = new Dictionary<string, TH1RuneMBonusJson>();
            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/RuneMBonus.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<string, TH1RuneMBonusJson>>(json);
            foreach (KeyValuePair<string, TH1RuneMBonusJson> _item in _parseCollection)
            {
                TH1RuneMBonusJson _bonus = _item.Value;
                TH1RuneMBonus _tmpRuneBonus = new TH1RuneMBonus(_item.Key, _bonus.name, _bonus.typeA, _bonus.typeB, _bonus.maxBonus);
                _collection.Add(_item.Key, _tmpRuneBonus);
            }

        }

        // Return Bonus
        public TH1RuneMBonus findBonus(string id)
        {
            _collection.TryGetValue(id, out TH1RuneMBonus tmpRes);
            return tmpRes;
        }

        // Other Functions
        public string[] bonusNameArray()
        {
            return bonusNameArray(char.Parse(""), false);
        }
        public string[] bonusNameArray( char prefix, bool incID )
        {
            List<TH1RuneMBonus> values = Enumerable.ToList(_collection.Values);
            List<string> tmpRes = new List<string>();
            for (int i = 0; i < values.Count; i++)
                if ((prefix == 0) || (prefix == values[i].bonusID[0]))
                {
                    string tmpLine = "";
                    if (incID) tmpLine = values[i].bonusNameID;
                    else tmpLine = values[i].bonusLongName;
                    tmpRes.Add(tmpLine);
                }
            return tmpRes.ToArray();
        }
    }

    #endregion TH1RuneM

    #region TH1RuneU

    public class TH1RuneU
    {
        // Constants
        // private int _tier = 1;
        // private int _statLevel = -1;

        // Private
        private int _id;
        private char _colourKey;
        private int _level;
        private TH1Mutation _mutation;
        private int _bonusDmg;
        private int _bonusDur;
        private double _bonusProc;
        private int _value;
        private int _cost;
        private string _name;
        private string _desc;

        // Construction
        public TH1RuneU( int Id, char ColourKey, int Level, TH1Mutation Mutation, int BonusDamage, int BonusDur, double BonusProc, int RuneValue, int CraftCost, string Name, string BonusDesc )
        {
            _id = Id;
            _colourKey = ColourKey;
            _level = Level;
            _mutation = Mutation;
            _bonusDmg = BonusDamage;
            _bonusDur = BonusDur;
            _bonusProc = BonusProc;
            _value = RuneValue;
            _cost = CraftCost;
            _name = Name;
            _desc = BonusDesc;
        }

        // Public
        public int runeID
        {
            get
            {
                return _id;
            }
        }
        public int runeLevel
        {
            get
            {
                return _level;
            }
        }
        public char runeColourKey
        {
            get
            {
                return _colourKey;
            }
        }
        public string runeName
        {
            get
            {
                return _name;
            }
        }
        public string runeDesc
        {
            get
            {
                return _desc;
            }
        }
        public string mutationName
        {
            get
            {
                return _mutation.description;
            }
        }
    }
    public class TH1RuneUJson
    {
        [JsonProperty("ID")] public int ID { get; set; }
        [JsonProperty("ColourKey")] public char ColourKey { get; set; }
        [JsonProperty("Level")] public int Level { get; set; }
        [JsonProperty("MutationID")] public int MutationID { get; set; }
        [JsonProperty("BonusDamage")] public int BonusDamage { get; set; }
        [JsonProperty("BonusDur")] public int BonusDur { get; set; }
        [JsonProperty("BonusProc")] public double BonusProc { get; set; }
        [JsonProperty("RuneValue")] public int RuneValue { get; set; }
        [JsonProperty("CraftCost")] public int CraftCost { get; set; }
        [JsonProperty("Name")] public string Name { get; set; }
        [JsonProperty("BonusDesc")] public string BonusDesc { get; set; }
    }
    public class TH1RuneUCollection
    {
        // The Collection
        private Dictionary<string, TH1RuneU> _collection = new Dictionary<string, TH1RuneU>();

        // Create
        public TH1RuneUCollection( TH1MutationCollection mutationCollection )
        {
            // Load The Library
            string json;
            Dictionary<string, TH1RuneUJson> _parseCollection = new Dictionary<string, TH1RuneUJson>();

            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/RuneUCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<string, TH1RuneUJson>>(json);
            foreach (KeyValuePair<string, TH1RuneUJson> _item in _parseCollection)
            {
                TH1RuneUJson _rune = _item.Value;
                TH1Mutation _mutation = mutationCollection.findMutation(_rune.MutationID);
                TH1RuneU _tmpRune = new TH1RuneU(_rune.ID, _rune.ColourKey, _rune.Level, _mutation, _rune.BonusDamage, _rune.BonusDur, _rune.BonusProc, _rune.RuneValue, _rune.CraftCost, _rune.Name, _rune.BonusDesc);
                _collection.Add(_item.Key, _tmpRune);
            }

        }

        // Return Rune
        public TH1RuneU findRune(char colourCode, int runeID)
        {
            return findRune(colourCode + "_U_" + runeID.ToString());
        }
        public TH1RuneU findRune(byte[] nullString)
        {
            char[] asciiChars = new char[Encoding.ASCII.GetCharCount(nullString, 0, nullString.Length - 1)];
            Encoding.ASCII.GetChars(nullString, 0, nullString.Length - 1, asciiChars, 0); // Remove Null
            string tmpString = new string(asciiChars); ;
            return findRune(tmpString);
        }
        public TH1RuneU findRune(string runeString)
        {
            _collection.TryGetValue(runeString, out TH1RuneU tmpRes);
            return tmpRes;
        }

    }

    #endregion TH1RuneU

    #region TH1Charm

    // Charms
    public class TH1Charm
    {
        // Private
        int _charmID;
        int _charmTier;
        TH1Mutation _mutation;
        int _charmLevel;
        string _charmName;
        List<int> _runeList;
        List<TH1CharmQuest> _questList;
        int _charmValue;
        TH1Mutation _mutationClass1;
        TH1Mutation _mutationClass2;
        int _minRuneLevel;
        int _charmIcon;
        double _charmBonusProc;
        int _charmBonusDamage;
        int _charmBonusDuration;

        // Public
        public int charmID
        {
            get
            {
                return _charmID;
            }
        }
        public int charmTier
        {
            get
            {
                return _charmTier;
            }
        }
        public TH1Mutation mutation
        {
            get
            {
                return _mutation;
            }
        }
        public int charmLevel
        {
            get
            {
                return _charmLevel;
            }
        }
        public string charmName
        {
            get
            {
                return _charmName;
            }
        }
        public List<int> runeList
        {
            get
            {
                return _runeList;
            }
        }
        public List<TH1CharmQuest> questList
        {
            get
            {
                return _questList;
            }
        }
        public int charmValue
        {
            get
            {
                return _charmValue;
            }
        }
        public TH1Mutation mutationClass1
        {
            get
            {
                return _mutationClass1;
            }
        } // ?
        public TH1Mutation mutationClass2
        {
            get
            {
                return _mutationClass2;
            }
        } // ?
        public int minRuneLevel
        {
            get
            {
                return _minRuneLevel;
            }
        }
        public int charmIcon
        {
            get
            {
                return _charmIcon;
            }
        }
        public double charmBonusProc
        {
            get
            {
                return _charmBonusProc;
            }
        }
        public int charmBonusDamage
        {
            get
            {
                return _charmBonusDamage;
            }
        }
        public int charmBonusDuration
        {
            get
            {
                return _charmBonusDuration;
            }
        }

        // Construction
        public TH1Charm(int ID, int Tier, TH1Mutation Mutation, int Level, string Name, string Runes, List<TH1CharmQuest> QuestList, int Value, TH1Mutation mClass1, TH1Mutation mClass2, int minLevel, int Icon, double BonusProc, int Damage, int Duration)
        {

            // Direct
            _charmID = ID;
            _charmTier = Tier;
            _mutation = Mutation;
            _charmLevel = Level;
            _charmName = Name;
            _charmValue = Value;
            _mutationClass1 = mClass1;
            _mutationClass2 = mClass2;
            _minRuneLevel = Level;
            _charmIcon = Icon;
            _charmBonusProc = BonusProc;
            _charmBonusDamage = Damage;
            _charmBonusDuration = Duration;
            _runeList = new List<int>();
            _questList = QuestList;

            // Parse
            string[] _tmpRList = Runes.Split(',');
            foreach (string _tmpR in _tmpRList) try { _runeList.Add(int.Parse(_tmpR)); } catch { }

        }
        public TH1Charm()
        {

            _charmID = 1;
            _charmTier = 1;
            _mutation = new TH1Mutation(19, "Vulnerability", "Melee Weakness");
            _charmLevel = 1;
            _charmName = "Epic Ruthless Inductor";
            _runeList = new List<int>{ 1,3,5,4,8 };
            _questList = new List<TH1CharmQuest> {
                new TH1CharmQuest(1,100,new TH1CharmQuestType(0,"Kill Any Enemy"),1),
                new TH1CharmQuest(36,3, new TH1CharmQuestType(5,"Kill Leaders"),1),
                new TH1CharmQuest(78,100, new TH1CharmQuestType(11,"Kill Enemies With Melee"), 1)
            };
            _charmValue = 1000;
            _mutationClass1 = _mutation;
            _mutationClass2 = _mutation;
            _minRuneLevel = 0;
            _charmIcon = 11;
            _charmBonusProc = 0.1;
            _charmBonusDamage = 0;
            _charmBonusDuration = 30;
        } // overload for default (valid rune)

        // Additional Public
        public string charmString
        {
            get
            {
                return string.Format("Grey_MutationQuestsTier{0}_{1}",charmTier.ToString(),charmID.ToString());
            }
        }
        public byte[] charmNullArray
        {
            get
            {
                string tmpString = charmString;
                byte[] tmpRes = new byte[tmpString.Length + 1];
                for (int i = 0; i < tmpString.Length; i++)
                {
                    tmpRes[i] = Convert.ToByte(tmpString[i]);
                }
                return tmpRes;
            }
        } // Null Terminated Array
        public BitmapImage image
        {
            get
            {
                BitmapImage tmpres;
                try
                {
                    tmpres = new BitmapImage(new Uri(string.Format("pack://application:,,,/Icons/Charms/C{0}.png", charmIcon)));
                }
                catch { tmpres = new BitmapImage(new Uri("pack://application:,,,/Icons/Charms/C0.png")); }
                return tmpres;
            }
        }
        public string mutationString
        {
            get
            {
                return mutation.description;
            }
        }
        public string charmLongName
        {
            get
            {
                return string.Format("{0}: L{1} {2}",_charmID, _charmLevel, _charmName);
            }
        }
    } 
    public class TH1CharmJson
    {
        [JsonProperty("String")] public string String { get; set; }
        [JsonProperty("ItemID")] public int ID { get; set; }
        [JsonProperty("Tier")] public int Tier { get; set; }
        [JsonProperty("Mutation")] public int Mutation { get; set; }
        [JsonProperty("Level")] public int Level { get; set; }
        [JsonProperty("Name")] public string Name { get; set; }
        [JsonProperty("Ingredients")] public string Runes { get; set; }
        [JsonProperty("QuestList")] public string Quests { get; set; }
        [JsonProperty("Value")] public int Value { get; set; }
        [JsonProperty("MutationClass1")] public int MutationClass1 { get; set; }
        [JsonProperty("MutationClass2")] public int MutationClass2 { get; set; }
        [JsonProperty("MinRuneLevel")] public int MinRuneLevel { get; set; }
        [JsonProperty("CharmIcon")] public int CharmIcon { get; set; }
        [JsonProperty("BonusProc")] public double BonusProc { get; set; }
        [JsonProperty("BonusDamage")] public int BonusDamage { get; set; }
        [JsonProperty("BonusDur")] public int BonusDur { get; set; }
    } 
    public class TH1CharmCollection
    {
        // The Collection
        private Dictionary<string, TH1Charm> _collection = new Dictionary<string, TH1Charm>();

        // Create
        public TH1CharmCollection( TH1CharmQuestCollection questCollection, TH1MutationCollection mutationCollection )
        {
            // Load The Library
            string json;
            Dictionary<string, TH1CharmJson> _parseCollection = new Dictionary<string, TH1CharmJson>();

            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/CharmCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<string, TH1CharmJson>>(json);
            foreach (KeyValuePair<string, TH1CharmJson> _item in _parseCollection)
            {
                TH1CharmJson _charm = _item.Value;

                List<TH1CharmQuest> questList = new List<TH1CharmQuest>();
                string[] tmpQuestList = _charm.Quests.Split(',');
                foreach( string quest in tmpQuestList) questList.Add(questCollection.findCharmQuest(int.Parse(quest)));

                TH1Charm _tmpCharm = new TH1Charm(
                    _charm.ID,  _charm.Tier, mutationCollection.findMutation(_charm.Mutation), _charm.Level, _charm.Name,
                    _charm.Runes, questList, _charm.Value, mutationCollection.findMutation(_charm.MutationClass1), mutationCollection.findMutation(_charm.MutationClass2), _charm.MinRuneLevel,
                    _charm.CharmIcon, _charm.BonusProc, _charm.BonusDamage,  _charm.BonusDur
                );
                _collection.Add(_item.Key, _tmpCharm);

            }

        }

        // Return Rune
        public TH1Charm findCharm(byte[] nullString)
        {
            char[] asciiChars = new char[Encoding.ASCII.GetCharCount(nullString, 0, nullString.Length - 1)];
            Encoding.ASCII.GetChars(nullString, 0, nullString.Length - 1, asciiChars, 0); // Remove Null
            string tmpString = new string(asciiChars); ;
            return findCharm(tmpString);
        }
        public TH1Charm findCharm(string charmString)
        {
            _collection.TryGetValue(charmString, out TH1Charm tmpRes);
            return tmpRes;
        }
        public TH1Charm findCharmByLongName(string longName)
        {
            foreach( KeyValuePair<string,TH1Charm> _kvp in _collection) {
                if (_kvp.Value.charmLongName == longName) return _kvp.Value;
            }
            return new TH1Charm();
        }

        // Check Rune
        public bool charmValid(string charmString)
        {
            return _collection.TryGetValue(charmString, out TH1Charm _tmp);
        }

        // Other Functions
        public string[] charmLongNamesArray
        {
            get {
                List<string> tmpRes = new List<string>();
                foreach (KeyValuePair<string, TH1Charm> _kvp in _collection) {
                    tmpRes.Add(_kvp.Value.charmLongName);
                }
                return tmpRes.ToArray();
            }
        }

        public string[] charmLongNamesByLevel( int level )
        {
            List<string> tmpRes = new List<string>();
            foreach (KeyValuePair<string, TH1Charm> _kvp in _collection)
            {
                if( _kvp.Value.charmLevel == level)
                tmpRes.Add(_kvp.Value.charmLongName);
            }
            return tmpRes.ToArray();
        }
    }
    // Charm Quests
    public class TH1CharmQuest
    {
        // Private
        int _questId;
        int _questTarget;
        TH1CharmQuestType _questType;
        int _questLevel;

        // Public
        public int questID
        {
            get
            {
                return _questId;
            }
        }
        public int questTarget
        {
            get
            {
                return _questTarget;
            }
        }
        public TH1CharmQuestType questType
        {
            get
            {
                return _questType;
            }
        }
        public int questLevel
        {
            get
            {
                return _questLevel;
            }
        }

        // Construction
        public TH1CharmQuest(int ID, int Target, TH1CharmQuestType Type, int Level)
        {
            _questId = ID;
            _questTarget = Target;
            _questType = Type;
            _questLevel = Level;
        }
        public TH1CharmQuest()
        {
            _questId = 1;
            _questTarget = 100;
            _questType = new TH1CharmQuestType();
            _questLevel = 1;
        } // overload for default (valid quest)
        public string questLongName
        {
            get
            {
                return string.Format("{0} (L{1}, Target {2})",questType.questTypeDesc,questLevel,questTarget);
            }
        }
    } 
    public class TH1CharmQuestJson
    {
        [JsonProperty("Quest_ID")] public int ID { get; set; }
        [JsonProperty("Goal")]   public int Target { get; set; }
        [JsonProperty("QuestType")] public int Type { get; set; }
        [JsonProperty("Quest Level")] public int Level { get; set; }
    } 
    public class TH1CharmQuestCollection
    {
        // The Collection
        private Dictionary<int, TH1CharmQuest> _collection = new Dictionary<int, TH1CharmQuest>();

        // Create
        public TH1CharmQuestCollection( TH1CharmQuestTypeCollection questTypeCollection )
        {
            // Load The Library
            string json;
            Dictionary<int, TH1CharmQuestJson> _parseCollection = new Dictionary<int, TH1CharmQuestJson>();

            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/CharmQuestCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<int, TH1CharmQuestJson>>(json);
            foreach (KeyValuePair<int, TH1CharmQuestJson> _item in _parseCollection)
            {
                TH1CharmQuestJson _cqj = _item.Value;
                TH1CharmQuestType _questType = questTypeCollection.findQuestType(_cqj.Type);
                TH1CharmQuest _tmpQuest = new TH1CharmQuest(_item.Key, _cqj.Target, _questType, _cqj.Level);
                _collection.Add(_item.Key, _tmpQuest);
            }

        }

        // Return Charm Quest
        public TH1CharmQuest findCharmQuest(int id)
        {
            _collection.TryGetValue(id, out TH1CharmQuest tmpRes);
            return tmpRes;
        }

        // Check Charm Quest
        public bool runeValid(int id)
        {
            return _collection.TryGetValue(id, out TH1CharmQuest _tmp);
        }
    }
    // Charm Quest Types
    public class TH1CharmQuestType
    {
        // Private
        int _questTypeId;
        string _questTypeDesc;

        // Public
        public int questTypeId
        {
            get
            {
                return _questTypeId;
            }
        }
        public string questTypeDesc
        {
            get
            {
                return _questTypeDesc;
            }
        }
 
        // Construction
        public TH1CharmQuestType(int id, string desc)
        {
            _questTypeId = id;
            _questTypeDesc = desc;
        }
        public TH1CharmQuestType()
        {
            _questTypeId = 0;
            _questTypeDesc = "Kill Any Enemy";
        } // overload for default (valid quest type)

    }
    public class TH1CharmQuestTypeJson
    {
        [JsonProperty("ID")] public int ID { get; set; }
        [JsonProperty("Description")] public string Description { get; set; }
    } 
    public class TH1CharmQuestTypeCollection
    {
        // The Collection
        private Dictionary<int, TH1CharmQuestType> _collection = new Dictionary<int, TH1CharmQuestType>();

        // Create
        public TH1CharmQuestTypeCollection()
        {
            // Load The Library
            string json;
            Dictionary<int, TH1CharmQuestTypeJson> _parseCollection = new Dictionary<int, TH1CharmQuestTypeJson>();

            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/CharmQuestTypeCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<int, TH1CharmQuestTypeJson>>(json);
            foreach (KeyValuePair<int, TH1CharmQuestTypeJson> _item in _parseCollection)
            {
                TH1CharmQuestTypeJson _cqtj = _item.Value;
                TH1CharmQuestType _tmpType = new TH1CharmQuestType(_item.Key, _cqtj.Description);
                _collection.Add(_item.Key, _tmpType);
            }

        }

        // Return Quest Type
        public TH1CharmQuestType findQuestType(int id)
        {
            _collection.TryGetValue(id, out TH1CharmQuestType tmpRes);
            return tmpRes;
        }

        // Check Quest Type
        public bool questTypeValid(int id)
        {
            return _collection.TryGetValue(id, out TH1CharmQuestType tmpRes);
        }
    }
    // Charms In Storage
    public class TH1CharmExt
    {
        private TH1Charm _charm;
        private uint _val2;
        private uint _valueModifier;
        private bool _inActiveSlot;
        private uint _val8;
        private uint _progress;
        private uint _activeQuestId;

        TH1CharmQuest _activeQuest;
        private List<bool> _runesReq = new List<bool>();

        // Construction
        public TH1CharmExt(TH1Charm Charm)
        {
            _charm = Charm;
            if(isEquip) if (_activeQuestId == 0) activeQuestId = (uint)_charm.questList[0].questID;
            _valueModifier = 10000;
        }

        // Public
        public TH1Charm charm
        {
            get
            {
                return _charm;
            }
        }

        // #1 & #5 
        public bool alwaysTrue
        {
            get
            {
                return true;
            }
        }
        public uint alwaysTrueUint
        {
            get
            {
                return alwaysTrue ? 1u : 0u;
            }
        }
        // #2
        public uint val2
        {
            get
            {
                return _val2;
            }
            set
            {
                _val2 = value;
            }
        }
        // #3
        public uint valueModifier
        {
            get
            {
                return _valueModifier;
            }
            set
            {
                _valueModifier = value;
            }
        }
        // #4 & #6
        public bool inActiveSlot
        {
            set
            {
                _inActiveSlot = value;
            }
            get
            {
                return _inActiveSlot;
            }
        }
        public uint inActiveSlotUint
        {
            get
            {
                return _inActiveSlot ? 1u : 0u;
            }
        }
        // #7
        public bool goalComplete
        {
            get
            {
                return isEquip && (progress == target);
            }
        }
        public uint goalCompleteUint
        {
            get
            {
                return goalComplete ? 1u : 0u;
            }
        }
        public uint val8
        {
            get
            {
                return _val8;
            }
            set
            {
                _val8 = value;
            }
        }
        public uint progress
        {
            get
            {
                if (!isEquip) return 0;
                if (_progress > activeQuest.questTarget) return (uint)activeQuest.questTarget;
                else return _progress;
            }
            set
            {
                _progress = value;
            }
        }
        public uint activeQuestId
        {
            get
            {
                return _activeQuestId;
            }
            set
            {
                _activeQuestId = value;
                foreach (TH1CharmQuest _quest in _charm.questList)
                    if (_quest.questID == _activeQuestId) _activeQuest = _quest;
            }
        }

        // Other Functions
        public List<bool> runesReq
        {
            get
            {
                return _runesReq;
            }
            set
            {
                _runesReq = value;
            }
        }
        public byte[] charmToActiveArray
        {
            get
            {
                return charmToArray(true);
            }
        }
        public byte[] charmToInventryArray
        {
            get
            {
                return charmToArray(false);
            }
        }
        public TH1CharmQuest activeQuest
        {
            get
            {

                return _activeQuest;
            }
        }
        public string charmName
        {
            get
            {
                if (_charm != null) return _charm.charmName;
                else return "None";
            }
        }
        public string charmLongName
        {
            get
            {
                if (!isEquip) return "None";
                return _charm.charmLongName;
            }
        }
        public bool isEquip
        {
            get
            {
                return _charm != null;
            }
        }
        public bool isComplete
        {
            get
            {
                if (!isEquip) return false;
                if ((target > 0) && (progress != target)) return false;
                if (runesReq.ToArray().Where(c => c).Count() < _charm.runeList.Count) return false;
                return true;
            }
        }
        public int target
        {
            get
            {
                if (!isEquip) return 0;
                else return activeQuest.questTarget;
            }
        }
        public BitmapImage image
        {
            get
            {
                if (!isEquip) return new BitmapImage(new Uri("pack://application:,,,/Icons/Charms/C0.png"));
                else return _charm.image;
            }
        }
        public string questName
        {
            get
            {
                if (!isEquip) return "";
                else return activeQuest.questType.questTypeDesc;
            }
        }
        public string questLongName
        {
            get
            {
                if (questName == "") return "";
                else return activeQuest.questLongName;
            }
        }
        public string[] questsLongNameArray
        {
            get
            {
                List<string> _tmpres = new List<string>();
                if (!isEquip) return _tmpres.ToArray();
                foreach( TH1CharmQuest _quest in _charm.questList)
                {
                    _tmpres.Add(_quest.questLongName);
                }
                return _tmpres.ToArray();
            }
        }
        public string mutationString
        {
            get
            {
                if (_charm != null) return _charm.mutationString;
                else return "None";
            }
        }
        public string charmString
        {
            get
            {
                if (_charm == null) return "NOID";
                else return string.Format("Grey_MutationQuestsTier{0}_{1}", _charm.charmTier, _charm.charmID);
            }
        }
        public double goalPerc
        {
            get
            {
                if (!isEquip || (target == 0)) return 0;
                return (double)progress / (double)target;
            }
        }
        public string goalPercString
        {
            get
            {
                return string.Format("{0:P1}", goalPerc);
            }
        }
        public int runesRequiredCount
        {
            get
            {
                if (!isEquip) return 0;
                return _charm.runeList.Count;
            }
        }
        public int runesInsertedCount
        {
            get
            {
                if (!isEquip) return 0;
                return _runesReq.ToArray().Where(c => c).Count();
            }
        }
        public int runesRemainingCount
        {
            get
            {
                return runesRequiredCount - runesInsertedCount;
            }
        }
        public string runesInsertedString
        {
            get
            {
                if (!isEquip) return "N/A";
                return string.Format("{0}/{1}",runesInsertedCount,runesRequiredCount);
            }
        }
        public int charmLevel
        {
            get
            {
                if (!isEquip) return 0;
                return _charm.charmLevel;
            }
        }
        public string exDataString
        {
            get
            {
                return string.Format("{0},{1}", _val2, _val8);
            }
        }
        public int calcValue
        {
            get
            {
                try
                {
                    // dial m for deciMal
                    if (!isEquip) return 0;
                    try
                    {
                        decimal tmpVal = (charm.charmValue * (1m / 4m)) * (valueModifier / 10000m);
                        return Convert.ToInt32(tmpVal);
                    } catch { return 0; }
                }
                catch { return 0; }
            }
        }

        // Private Helpers
        private byte[] charmToArray( bool isactive)
        {
            int valueCount = 10;
            int runesReqCount = runesReq.Count;
            int dataLength = 0;

            if (_charm != null)
            {
                dataLength = (valueCount * 4) + 4 + (runesReqCount * 4);
            }

            int bufferLength = 4 + charmString.Length + 1 + dataLength;
            if (!isactive) bufferLength += 8;

            byte[] tmpRune = new byte[bufferLength];
            RWStream writer = new RWStream(tmpRune, true, true);
            try
            {
                // Charm String
                writer.WriteUInt32((uint)charmString.Length + 1);
                writer.WriteString(charmString, StringType.Ascii, charmString.Length);
                writer.WriteBytes(new byte[] { 0x00 });

                // Separator
                if(!isactive) writer.WriteBytes(new byte[] {
                        0x00, 0x00, 0x00, 0x00,
                        0x12, 0x34, 0x56, 0x78 // Old Friend
                    });

                if (_charm != null)
                {
                    // Charm Values
                    writer.WriteUInt32(alwaysTrueUint);     // #1
                    writer.WriteUInt32(val2);               // #2 ??
                    writer.WriteUInt32(valueModifier);      // #3
                    writer.WriteUInt32(inActiveSlotUint);   // #4
                    writer.WriteUInt32(alwaysTrueUint);     // #5
                    writer.WriteUInt32(inActiveSlotUint);   // #6
                    writer.WriteUInt32(goalCompleteUint);   // #7
                    writer.WriteUInt32(val8);               // #8 ??
                    writer.WriteUInt32(progress);           // #9
                    writer.WriteUInt32(activeQuestId);      // #10

                    // Charm RuneReq
                    writer.WriteUInt32((uint)runesReq.Count);
                    for (int i = 0; i < runesReq.Count; i++)
                    {
                        writer.WriteUInt32((uint)(runesReq.ElementAt(i) ? 1 : 0));
                    }
                }

            }
            catch { }
            finally { writer.Flush(); tmpRune = writer.ReadAllBytes(); writer.Close(true); }
            return tmpRune;
        }
    }

    #endregion TH1Charm

    #region TH1Obelisk
    public class TH1Obelisk
    {
        private uint _key;
        private bool _value;

        public TH1Obelisk(uint Key, uint Value)
        {
            _key = Key;
            _value = Value == 1;
        }

        public uint key
        {
            get
            {
                return _key;
            }
        }
        public bool value
        {
            get
            {
                return _value;
            }
        }
        public uint valueUint
        {
            get
            {
                return _value ? 1u : 0u;
            }
        }
    }

    #endregion TH1Obelisk

    #region TH1Mutation

    public class TH1Mutation
    {
        private int _id;
        private string _name;
        private string _description;

        public int id
        {
            get
            {
                return _id;
            }
        }
        public string name
        {
            get
            {
                return _name;
            }
        }
        public string description
        {
            get
            {
                return _description;
            }
        }

        public TH1Mutation (int Id, string Name, string Description)
        {
            _id = Id;
            _name = Name;
            _description = Description;
        }
    }
    public class TH1MutationJson
    {
        [JsonProperty("Name")] public string Name { get; set; }
        [JsonProperty("Description")] public string Description { get; set; }
    }
    public class TH1MutationCollection
    {
        // The Collection
        private Dictionary<int, TH1Mutation> _collection = new Dictionary<int, TH1Mutation>();

        // Create
        public TH1MutationCollection()
        {
            // Load The Library
            string json;
            Dictionary<int, TH1MutationJson> _parseCollection = new Dictionary<int, TH1MutationJson>();

            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/MutationCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<int, TH1MutationJson>>(json);
            foreach (KeyValuePair<int, TH1MutationJson> _item in _parseCollection)
            {
                TH1MutationJson _mj = _item.Value;
                TH1Mutation _tmpType = new TH1Mutation(_item.Key, _mj.Name, _mj.Description);
                _collection.Add(_item.Key, _tmpType);
            }

        }

        // Return Quest Type
        public TH1Mutation findMutation(int id)
        {
            _collection.TryGetValue(id, out TH1Mutation tmpRes);
            return tmpRes;
        }
        // Check Quest Type
        public bool mutationValid(int id)
        {
            return _collection.TryGetValue(id, out TH1Mutation tmpRes);
        }
    }

    #endregion TH1Mutation

    #region TH1Weapon

    public class TH1Weapon
    {
        // Private
        private string _string;
        private char _colourKey;
        private int _alignment;
        private int _id;
        private int _aIndex;
        private int _aIndex2;
        private int _type;
        private List<string> _runes;
        private int _level;
        private int _smLevel;
        private string _smId;
        private int _smN;
        private int _value;
        private string _prefix;
        private string _noun;
        private int _charClass;
        private int _condition;
        private TH1Paint _paint;
        private bool _isElite;
        private List<string> _bonusDesc;
        private int _suitId;
        private string _eliteBonusDesc;
        // Additional
        private string _charClassName;
        private string _alignmentName;
        private bool _isEliteSuit;
        private int _runesInserted;

        // Construction
        public TH1Weapon( 
            string String, char ColourKey, int Alignment, int Id, int AIndex, int AIndex2, int Type,
            string Rune1, string Rune2, string Rune3, string Rune4, int Level, int SmLevel,
            string SmId, int SmN, int Value, string Prefix, string Noun, int Class,
            int Condition, TH1Paint Paint, int Elite, string Bonus1, string Bonus2, string Bonus3,
            string Bonus4, int SuitId, string EliteBonus )
        {
            _string = String;
            _colourKey = ColourKey;
            _alignment = Alignment;
            _id = Id;
            _aIndex = AIndex;
            _aIndex2 = AIndex2;
            _type = Type;
            _runes = new List<string> { Rune1, Rune2, Rune3, Rune4 };
            _level = Level;
            _smLevel = SmLevel;
            _smId = SmId;
            _smN = SmN;
            _value = Value;
            _prefix = Prefix;
            _noun = Noun;
            _charClass = Class;
            _condition = Condition;
            _paint = Paint;
            _isElite = Elite == 1;
            _bonusDesc = new List<string> { Bonus1, Bonus2, Bonus3, Bonus4 };
            _suitId = SuitId;
            _eliteBonusDesc = EliteBonus;
            //
            for (int i = 0; i < _runes.Count; i++) if (runes[i] == "")
                {
                    runes.RemoveAt(i);
                    i--;
                }
            if (_charClass > -1) _charClassName = new TH1Helper().classNamesArray[_charClass];
            else _charClassName = "Any";
            _alignmentName = new TH1Helper().alignmentNamesArray[_alignment];
            _isEliteSuit = _isElite && (_suitId>0);
            _runesInserted = _runes.Count;
        }

        public TH1Weapon()
        {
            _string = "GreyGeneric1_Weapons_1";
            _colourKey = 'G';
            _alignment = 0;
            _id = 1;
            _aIndex = 1;
            _aIndex2 = 0;
            _type = 0;
            _runes = new List<string> { "", "", "", "" };
            _level = 1;
            _smLevel = 0;
            _smId = "";
            _smN = 0;
            _value = 40;
            _prefix = "Ceremonial";
            _noun = "Seax";
            _charClass = 0;
            _condition = 10000;
            _paint = new TH1Paint(70, "Faded Red", "Grey (levels 1-25)");
            _isElite = false;
            _bonusDesc = new List<string> { "", "", "", "" };
            _suitId = 0;
            _eliteBonusDesc = "";
            //
            for (int i = 0; i < _runes.Count; i++) if (runes[i] == "")
                {
                    runes.RemoveAt(i);
                    i--;
                }
            if (_charClass > -1) _charClassName = new TH1Helper().classNamesArray[_charClass];
            else _charClassName = "Any";
            _alignmentName = new TH1Helper().alignmentNamesArray[_alignment];
            _isEliteSuit = _isElite && (_suitId > 0);
            _runesInserted = _runes.Count;
        }

        // Public
        public char colourKey
        {
            get
            {
                return _colourKey;
            }
        }
        public int alignment
        {
            get
            {
                return _alignment;
            }
        }
        public int id
        {
            get
            {
                return _id;
            }
        }
        public int aIndex {
            get
            {
                return _aIndex;
            }
        }
        public int aIndex2
        {
            get
            {
                return _aIndex2;
            }
        }
        public int type
        {
            get
            {
                return _type;
            }
        }
        public List<string> runes
        {
            get
            {
                return _runes;
            }
        }
        public int level
        {
            get
            {
                return _level;
            }
        }
        public int smLevel
        {
            get
            {
                return _smLevel;
            }
        }
        public string smId
        {
            get
            {
                return _smId;
            }
        }
        public int smN
        {
            get
            {
                return _smN;
            }
        }
        public int value
        {
            get
            {
                return _value;
            }
        }
        public string prefix
        {
            get
            {
                return _prefix;
            }
        }
        public string noun
        {
            get
            {
                return _noun;
            }
        }
        public int charClass
        {
            get
            {
                return _charClass;
            }
        }
        public int condition
        {
            get
            {
                return _condition;
            }
        }
        public TH1Paint paint
        {
            get
            {
                return _paint;
            }
        }
        public bool isElite
        {
            get
            {
                return _isElite;
            }
        }
        public List<string> bonusDesc
        {
            get
            {
                return _bonusDesc;
            }
        }
        public int suitId
        {
            get
            {
                return _suitId;
            }
        }
        public string eliteBonusDesc
        {
            get
            {
                return _eliteBonusDesc;
            }
        }

        // More !
        public string weaponName
        {
            get
            {
                string tmpPrefix = "";
                string tmpElite = "";
                if (_prefix != "") tmpPrefix = _prefix + " ";
                if (_isEliteSuit) tmpElite = string.Format("(Elite Suit #{0}) ",_suitId);
                else if (_isElite) tmpElite = "(Elite) ";
                return string.Format("{2}{0}{1}", tmpPrefix, _noun,tmpElite);
            }
        }
        public string weaponLongName
        {
            get
            {
                return string.Format("L{0} {1}", level, weaponName);
            }
        }
        public string weaponLongIdName
        {
            get
            {
                return string.Format("{0}: {1}",id, weaponLongName);
            }
        }
        public BitmapImage image
        {
            get
            {
                BitmapImage tmpres;
                try
                {
                    tmpres = new BitmapImage(new Uri(string.Format("pack://application:,,,/Icons/Weapons/{0}{1}.png", colourKey, type)));
                }
                catch { tmpres = new BitmapImage(new Uri("pack://application:,,,/Icons/Weapons/default.png")); }
                return tmpres;
            }
        }
        public string charClassName
        {
            get
            {
                return _charClassName;
            }
        }
        public string alignmentName
        {
            get
            {
                return _alignmentName;
            }
        }
        public bool isEliteSuit
        {
            get
            {
                return _isEliteSuit;
            }
        }
        public int emptyRuneSlots
        {
            get
            {
                int maxslots = 0;
                switch (_colourKey) {
                    case 'R' :
                    case 'O': maxslots = 4;
                        break;
                    case 'P': maxslots = 3;
                        break;
                    case 'B': maxslots = 2;
                        break;
                    case 'E': maxslots = 1;
                        break;
                    default: maxslots = 0;
                        break;
                }
                return (maxslots - _runesInserted); // Math.Max(maxslots - _runesInserted, 0);
            }
        }
        public int weaponTier
        {
            get
            {
                return Math.Max(((int)(_level / 5))*5,1);
            }
        }
        public string weaponString
        {
            get
            {
                return _string;
            }
        }
    }
    public class TH1WeaponJson
    {
        [JsonProperty("Colour Key")] public char ColourKey { get; set; }
        [JsonProperty("Alignment")] public int Alignment { get; set; }
        [JsonProperty("ItemID")] public int ItemID { get; set; }
        [JsonProperty("AugIndex")] public int AugIndex { get; set; }
        [JsonProperty("SecondaryAugIndex")] public int SecondaryAugIndex { get; set; }
        [JsonProperty("Type")] public int Type { get; set; }
        [JsonProperty("Bonus1")] public string Bonus1 { get; set; }
        [JsonProperty("Bonus2")] public string Bonus2 { get; set; }
        [JsonProperty("Bonus3")] public string Bonus3 { get; set; }
        [JsonProperty("Bonus4")] public string Bonus4 { get; set; }
        [JsonProperty("Level")] public int Level { get; set; }
        [JsonProperty("Multiplier")] public decimal Multiplier { get; set; }
        [JsonProperty("SuperMoveLevel")] public int SuperMoveLevel { get; set; }
        [JsonProperty("SuperMoveID")] public string SuperMoveID { get; set; }
        [JsonProperty("SMN")] public int SMN { get; set; }
        [JsonProperty("Value")] public int Value { get; set; }
        [JsonProperty("Prefix1")] public string Prefix1 { get; set; }
        [JsonProperty("Noun")] public string Noun { get; set; }
        [JsonProperty("Class")] public int Class { get; set; }
        [JsonProperty("Condition")] public int Condition { get; set; }
        [JsonProperty("Color")] public int Color { get; set; }
        [JsonProperty("IsElite")] public int IsElite { get; set; }
        [JsonProperty("Bonus Desc 1")] public string BonusDesc1 { get; set; }
        [JsonProperty("Bonus Desc 2")] public string BonusDesc2 { get; set; }
        [JsonProperty("Bonus Desc 3")] public string BonusDesc3 { get; set; }
        [JsonProperty("Bonus Desc 4")] public string BonusDesc4 { get; set; }
        [JsonProperty("SuitID")] public int SuitID { get; set; }
        [JsonProperty("Elite Bonus Desc")] public string EliteBonusDesc { get; set; }
    } // Wow ..
    public class TH1WeaponCollection
    {
        Dictionary<string, TH1Weapon> _collection = new Dictionary<string, TH1Weapon>();

        public TH1WeaponCollection( TH1PaintCollection paintCollection )
        {
            string json;
            Dictionary<string, TH1WeaponJson> _parseCollection = new Dictionary<string, TH1WeaponJson>();
            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/WeaponCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib,Encoding.UTF8).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<string, TH1WeaponJson>>(json);
            foreach (KeyValuePair<string, TH1WeaponJson> _item in _parseCollection)
            {
                TH1WeaponJson _weapon = _item.Value;
                TH1Weapon _tmpRuneBonus = new TH1Weapon(
                    _item.Key, _weapon.ColourKey, _weapon.Alignment, _weapon.ItemID, _weapon.AugIndex,
                    _weapon.SecondaryAugIndex, _weapon.Type, _weapon.Bonus1, _weapon.Bonus2, _weapon.Bonus3, _weapon.Bonus4,
                    _weapon.Level, _weapon.SuperMoveLevel, _weapon.SuperMoveID, _weapon.SMN, _weapon.Value,
                    _weapon.Prefix1, _weapon.Noun, _weapon.Class, _weapon.Condition, paintCollection.findPaint(_weapon.Color),
                    _weapon.IsElite, _weapon.BonusDesc1, _weapon.BonusDesc2, _weapon.BonusDesc3, _weapon.BonusDesc4,
                    _weapon.SuitID, _weapon.EliteBonusDesc
                    );
                _collection.Add(_item.Key, _tmpRuneBonus);
            }

        }

        // Return Weapon
        public TH1Weapon findWeapon(byte[] nullString)
        {
            char[] asciiChars = new char[Encoding.ASCII.GetCharCount(nullString, 0, nullString.Length - 1)];
            Encoding.ASCII.GetChars(nullString, 0, nullString.Length - 1, asciiChars, 0); // Remove Null
            string tmpString = new string(asciiChars); ;
            return findWeapon(tmpString);
        }
        public TH1Weapon findWeapon(string weaponString)
        {
            _collection.TryGetValue(weaponString.Replace("\0", string.Empty), out TH1Weapon tmpRes);
            if (tmpRes == null)
            {
                MessageBox.Show("Could Not Find Weapon '" + weaponString + "' In The Collection.");
                tmpRes = new TH1Weapon();
            }
            return tmpRes;
        }
        public List<TH1Weapon> listWeapons(char colourKey, int weaponType, int characterClass, int alignment)
        {
            List<TH1Weapon> tmpRes = new List<TH1Weapon>();
            foreach (TH1Weapon weap in _collection.Values)
            {
                if (weap.colourKey == colourKey && weap.type == weaponType && (characterClass == -1 || weap.charClass == characterClass) && (alignment == -1 || weap.alignment == alignment))
                {
                    tmpRes.Add(weap);
                }
            }
            return tmpRes;
        }
        public List<string> listWeaponsByName( char colourKey, int weaponType, int characterClass, int alignment)
        {
            List<string> tmpRes = new List<string>();
            foreach( TH1Weapon weap in _collection.Values )
            {
                if (weap.colourKey == colourKey && weap.type == weaponType && (characterClass == -1 || weap.charClass == characterClass) && (alignment == -1 || weap.alignment == alignment))
                {
                    tmpRes.Add(weap.weaponLongIdName);
                }
            }
            return tmpRes;
        }
    }
    public class TH1WeaponExt
    {
        // Private
        private TH1Weapon _weapon;
        private bool _crafted; // always true?
        private uint _valueB; // boolean?
        private uint _condition;
        private bool _isEquipt; // always false?
        private TH1Paint _paint;
        private List<TH1RuneMExt> _runesInserted;

        // Public
        public TH1WeaponExt( TH1Weapon weapon )
        {
            _weapon = weapon;
            _paint = _weapon.paint;
            _condition = (uint)_weapon.condition;
            _crafted = true;
            _isEquipt = false;
            _runesInserted = new List<TH1RuneMExt>();
        }

        public TH1Weapon weapon
        {
            get
            {
                return _weapon;
            }
        }
        public bool crafted
        {
            get
            {
                return _crafted;
            }
            set
            {
                _crafted = value;
            }
        }
        public uint craftedUint
        {
            get
            {
                return _crafted ? 1u : 0u;
            }
            set
            {
                _crafted = (value == 1);
            }
        }
        public uint valueB
        {
            get
            {
                return _valueB;
            }
            set
            {
                _valueB = value;
            }
        }
        public uint condition
        {
            get
            {
                return _condition;
            }
            set
            {
                _condition = value;
            }
        }
        public bool isEquipt
        {
            get
            {
                return _isEquipt;
            }
            set
            {
                _isEquipt = value;
            }
        }
        public uint isEquiptUint
        {
            get
            {
                return _isEquipt ? 1u : 0u;
            }
            
            set
            {
                _isEquipt = (value == 1u);
            }
        }
        public TH1Paint paint
        {
            get
            {
                return _paint;
            }
            set
            {
                _paint = value;
            }
        }
        public List<TH1RuneMExt> runesInserted
        {
            get
            {
                return _runesInserted;
            }

            set
            {
                _runesInserted = value;
            }
        }
        public List<string> bonusRunes
        {
            get
            {
                return _weapon.runes;
            }
        }

        // Additional Publics
        public int weaponType
        {
            get
            {
                return _weapon.type;
            }
        }
        public string weaponTypeName
        {
            get
            {
                return new TH1Helper().weaponTypesDic.Values.ToArray()[_weapon.type];
            }
        }
        public string weaponName
        {
            get
            {
                return _weapon.weaponName;
            }
        }
        public string weaponLongName
        {
            get
            {
                return _weapon.weaponLongName;
            }
        }
        public string weaponLongIdName
        {
            get
            {
                return _weapon.weaponLongIdName;
            }
        }
        public int maxCondition
        {
            get
            {
                return _weapon.condition;
            }
        }
        public decimal conditionPerc
        {
            get
            {
                return (decimal)condition / (decimal)_weapon.condition;
            }
        }
        public char colourKey
        {
            get
            {
                return _weapon.colourKey;
            }
        }
        public int alignment
        {
            get
            {
                return _weapon.alignment;
            }
        }
        public string alignmentName
        {
            get
            {
                if (_weapon.alignment > -1) return new TH1Helper().alignmentNamesArray[_weapon.alignment];
                else return "None";
            }
        }
        public int level
        {
            get
            {
                return _weapon.level;
            }
        }
        public int charClass
        {
            get
            {
                return _weapon.charClass;
            }
        }
        public string charClassName
        {
            get
            {
                return _weapon.charClassName;
            }
        }
        public BitmapImage image
        {
            get
            {
                return _weapon.image;
            }
        }
        public int emptyRuneSlots
        {
            get
            {
                return _weapon.emptyRuneSlots;
            }
        }
        public int freeRuneSlots
        {
            get
            {
                return _weapon.emptyRuneSlots - runesInserted.Count;
            }
        }
        public string freeRuneOfEmpty
        {
            get
            {
                if (emptyRuneSlots == 0) return "None";
                else return string.Format("{0}/{1}",freeRuneSlots, emptyRuneSlots);
            }
        }
        public byte[] weaponExtToArray
        {
            get
            {
                string tmpName = _weapon.weaponString;
                int runesArrayLength = 0;
                foreach(TH1RuneMExt tmpRune in _runesInserted) runesArrayLength += tmpRune.runeExtToArray.Length;

                int tmpLength =
                    4 +                 // Name Length
                    tmpName.Length +    // Name
                    1 +                 // Null Byte
                    4 +                 // Count Runes Inserted
                    runesArrayLength +  // Runes Inserted
                    4 +                 // Header
                    (4 * 5);            // Ext Data

                byte[] tmpWeapon = new byte[tmpLength];
                RWStream writer = new RWStream(tmpWeapon, true, true);
                try
                {
                    // Weapon String
                    writer.WriteUInt32((uint)tmpName.Length + 1);
                    writer.WriteString(tmpName, StringType.Ascii, tmpName.Length);
                    writer.WriteBytes(new byte[] { 0x00 });

                    // Runes Inserted
                    writer.WriteUInt32((uint)_runesInserted.Count);
                    foreach (TH1RuneMExt tmpRune in _runesInserted)
                        writer.WriteBytes(tmpRune.runeExtToArray);
                    
                    // Weapon Ext
                    writer.WriteBytes(new byte [] { 0x12, 0x34, 0x56, 0x78});
                    writer.WriteUInt32(craftedUint);
                    writer.WriteUInt32(_valueB);
                    writer.WriteUInt32(_condition);
                    writer.WriteUInt32(isEquiptUint);
                    writer.WriteUInt32((uint)paint.paintID);
                }
                catch { }
                finally { writer.Flush(); tmpWeapon = writer.ReadAllBytes(); writer.Close(true); }
                return tmpWeapon;
            }
        }
        public string classAlignString
        {
            get
            {
                string prefix = "";
                if (alignmentName != "None") prefix = string.Format("{0}", alignmentName);
                else prefix = "Any";
                if (prefix == charClassName) return "Generic";
                else
                    if (charClassName != "Any") return string.Format("{0} {1}", prefix, charClassName);
                    else return string.Format("{1} {0}", prefix, charClassName);
            }
        }
    }

    #endregion Weapon

    #region TH1Armour

    public class TH1Armour
    {
        // Private
        private string _string;
        private char _colourKey;
        private int _alignment;
        private int _id;
        private int _aIndex;
        private int _tier;
        private int _type;
        private List<string> _runes;
        private int _level;
        private decimal _multiplier;
        private int _value;
        private string _prefix;
        private string _noun;
        private int _charClass;
        private int _condition;
        private TH1Paint _paint;
        private bool _isElite;
        private List<string> _bonusDesc;
        private int _suitId;
        private string _properName;
        // Additional
        private string _charClassName;
        private string _alignmentName;
        private bool _isEliteSuit;
        private int _runesInserted;

        // Construction
        public TH1Armour(
            string String, char ColourKey, int Alignment, int Id, int AIndex, int Tier, int Type,
            string Rune1, string Rune2, string Rune3, string Rune4, int Level, decimal Multiplier, int Value, string Prefix, string Noun, int Class,
            int Condition, TH1Paint Paint, int Elite, string Bonus1, string Bonus2, string Bonus3,
            string Bonus4, int SuitId, string ProperName)
        {
            _string = String;
            _colourKey = ColourKey;
            _alignment = Alignment;
            _id = Id;
            _aIndex = AIndex;
            _tier = Tier;
            _type = Type;
            _runes = new List<string> { Rune1, Rune2, Rune3, Rune4 };
            _level = Level;
            _multiplier = Multiplier;
            _value = Value;
            _prefix = Prefix;
            _noun = Noun;
            _charClass = Class;
            _condition = Condition;
            _paint = Paint;
            _isElite = Elite == 1;
            _bonusDesc = new List<string> { Bonus1, Bonus2, Bonus3, Bonus4 };
            _suitId = SuitId;
            _properName = ProperName;
            //
            for (int i = 0; i < _runes.Count; i++) if (runes[i] == "")
                {
                    runes.RemoveAt(i);
                    i--;
                }
            if (_charClass > -1) _charClassName = new TH1Helper().classNamesArray[_charClass];
            else _charClassName = "Any";
            _alignmentName = new TH1Helper().alignmentNamesArray[_alignment];
            _isEliteSuit = _isElite && (_suitId > 0);
            _runesInserted = _runes.Count;
        }

        public TH1Armour()
        {
            _string = "GreyGeneric1_Armor_1";
            _colourKey = 'G';
            _alignment = 0;
            _id = 1;
            _aIndex = 41;
            _tier = 1;
            _type = 0;
            _runes = new List<string> { "", "", "", "" };
            _level = 1;
            _value = 40;
            _prefix = "Ceremonial";
            _noun = "Ballistic Tac-Vest";
            _charClass = -1;
            _condition = 10000;
            _paint = new TH1Paint(69, "Champion", "");
            _isElite = false;
            _bonusDesc = new List<string> { "", "", "", "" };
            _suitId = 0;
            _properName = "";
            //
            for (int i = 0; i < _runes.Count; i++) if (runes[i] == "")
                {
                    runes.RemoveAt(i);
                    i--;
                }
            if (_charClass > -1) _charClassName = new TH1Helper().classNamesArray[_charClass];
            else _charClassName = "Any";
            _alignmentName = new TH1Helper().alignmentNamesArray[_alignment];
            _isEliteSuit = _isElite && (_suitId > 0);
            _runesInserted = _runes.Count;
        }

        // Public
        public char colourKey
        {
            get
            {
                return _colourKey;
            }
        }
        public int alignment
        {
            get
            {
                return _alignment;
            }
        }
        public int id
        {
            get
            {
                return _id;
            }
        }
        public int aIndex
        {
            get
            {
                return _aIndex;
            }
        }
        public int tier
        {
            get
            {
                return _tier;
            }
        }
        public int type
        {
            get
            {
                return _type;
            }
        }
        public List<string> runes
        {
            get
            {
                return _runes;
            }
        }
        public int level
        {
            get
            {
                return _level;
            }
        }
        public int value
        {
            get
            {
                return _value;
            }
        }
        public string prefix
        {
            get
            {
                return _prefix;
            }
        }
        public string noun
        {
            get
            {
                return _noun;
            }
        }
        public int charClass
        {
            get
            {
                return _charClass;
            }
        }
        public int condition
        {
            get
            {
                return _condition;
            }
        }
        public TH1Paint paint
        {
            get
            {
                return _paint;
            }
        }
        public bool isElite
        {
            get
            {
                return _isElite;
            }
        }
        public List<string> bonusDesc
        {
            get
            {
                return _bonusDesc;
            }
        }
        public int suitId
        {
            get
            {
                return _suitId;
            }
        }
        public string properName
        {
            get
            {
                return _properName;
            }
        }

        // More !
        public string armourName
        {
            get
            {
                string tmpPrefix = "";
                string tmpElite = "";
                if (_prefix != "") tmpPrefix = _prefix + " ";
                if (_isEliteSuit) tmpElite = string.Format("(Elite Suit #{0}) ", _suitId);
                else if (_isElite) tmpElite = "(Elite) ";
                return string.Format("{2}{0}{1}", tmpPrefix, _noun, tmpElite);
            }
        }
        public string armourLongName
        {
            get
            {
                return string.Format("L{0} {1}", level, armourName);
            }
        }
        public string armourLongIdName
        {
            get
            {
                return string.Format("{0}: {1}", id, armourLongName);
            }
        }
        public BitmapImage image
        {
            get
            {
                BitmapImage tmpres;
                try
                {
                    tmpres = new BitmapImage(new Uri(string.Format("pack://application:,,,/Icons/Armour/{0}{1}.png", colourKey, type)));
                }
                catch { tmpres = new BitmapImage(new Uri("pack://application:,,,/Icons/Armour/default.png")); }
                return tmpres;
            }
        }
        public string charClassName
        {
            get
            {
                return _charClassName;
            }
        }
        public string alignmentName
        {
            get
            {
                return _alignmentName;
            }
        }
        public bool isEliteSuit
        {
            get
            {
                return _isEliteSuit;
            }
        }
        public int emptyRuneSlots
        {
            get
            {
                int maxslots = 0;
                switch (_colourKey)
                {
                    case 'R':
                    case 'O':
                        maxslots = 4;
                        break;
                    case 'P':
                        maxslots = 3;
                        break;
                    case 'B':
                        maxslots = 2;
                        break;
                    case 'E':
                        maxslots = 1;
                        break;
                    default:
                        maxslots = 0;
                        break;
                }
                return (maxslots - _runesInserted); // Math.Max(maxslots - _runesInserted, 0);
            }
        }
        public int armourTier
        {
            get
            {
                return Math.Max(((int)(_level / 5)) * 5, 1);
            }
        }
        public string armourString
        {
            get
            {
                return _string;
            }
        }
    }
    public class TH1ArmourJson
    {
        [JsonProperty("Colour Key")] public char ColourKey { get; set; }
        [JsonProperty("Alignment")] public int Alignment { get; set; }
        [JsonProperty("ItemID")] public int ItemID { get; set; }
        [JsonProperty("AugIndex")] public int AugIndex { get; set; }
        [JsonProperty("Tier")] public int Tier { get; set; }
        [JsonProperty("Type")] public int Type { get; set; }
        [JsonProperty("Bonus1")] public string Bonus1 { get; set; }
        [JsonProperty("Bonus2")] public string Bonus2 { get; set; }
        [JsonProperty("Bonus3")] public string Bonus3 { get; set; }
        [JsonProperty("Bonus4")] public string Bonus4 { get; set; }
        [JsonProperty("Level")] public int Level { get; set; }
        [JsonProperty("Multiplier")] public decimal Multiplier { get; set; }
        [JsonProperty("Value")] public int Value { get; set; }
        [JsonProperty("Prefix1")] public string Prefix1 { get; set; }
        [JsonProperty("Noun")] public string Noun { get; set; }
        [JsonProperty("Class")] public int Class { get; set; }
        [JsonProperty("Condition")] public int Condition { get; set; }
        [JsonProperty("Color")] public int Color { get; set; }
        [JsonProperty("Bonus Desc 1")] public string BonusDesc1 { get; set; }
        [JsonProperty("Bonus Desc 2")] public string BonusDesc2 { get; set; }
        [JsonProperty("Bonus Desc 3")] public string BonusDesc3 { get; set; }
        [JsonProperty("Bonus Desc 4")] public string BonusDesc4 { get; set; }
        [JsonProperty("SuitId")] public int SuitID { get; set; }
        [JsonProperty("Proper Name")] public string ProperName { get; set; }

        public int IsElite
        {
            get
            {
                return (SuitID > 0) ? 1 : 0;
            }
        }
    } // Another Long One ..
    public class TH1ArmourCollection
    {
        Dictionary<string, TH1Armour> _collection = new Dictionary<string, TH1Armour>();

        public TH1ArmourCollection(TH1PaintCollection paintCollection, TH1ArmourBaseStatsCollection armourBaseStatsCollection)
        {
            string json;
            Dictionary<string, TH1ArmourJson> _parseCollection = new Dictionary<string, TH1ArmourJson>();
            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/ArmorCollection.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib, Encoding.UTF8).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<string, TH1ArmourJson>>(json);
            foreach (KeyValuePair<string, TH1ArmourJson> _item in _parseCollection)
            {
                TH1ArmourJson _armour = _item.Value;
                TH1Armour _tmpArmour = new TH1Armour(
                    _item.Key, _armour.ColourKey, _armour.Alignment, _armour.ItemID, _armour.AugIndex,
                    _armour.Tier, armourBaseStatsCollection.findBaseStats(_armour.AugIndex).armourType, 
                    _armour.Bonus1, _armour.Bonus2, _armour.Bonus3, _armour.Bonus4, _armour.Level,
                    _armour.Multiplier, _armour.Value, _armour.Prefix1, _armour.Noun, _armour.Class,
                    _armour.Condition, paintCollection.findPaint(_armour.Color), _armour.IsElite,
                    _armour.BonusDesc1, _armour.BonusDesc2, _armour.BonusDesc3, _armour.BonusDesc4,
                    _armour.SuitID, _armour.ProperName
                    );
                _collection.Add(_item.Key, _tmpArmour);
            }

        }

        // Return Weapon
        public TH1Armour findArmour(byte[] nullString)
        {
            char[] asciiChars = new char[Encoding.ASCII.GetCharCount(nullString, 0, nullString.Length - 1)];
            Encoding.ASCII.GetChars(nullString, 0, nullString.Length - 1, asciiChars, 0); // Remove Null
            string tmpString = new string(asciiChars);
            return findArmour(tmpString);
        }
        public TH1Armour findArmour(string armourString)
        {
            _collection.TryGetValue(armourString.Replace("\0",string.Empty), out TH1Armour tmpRes);
            if (tmpRes == null)
            {
                MessageBox.Show("Could Not Find Armour '" + armourString + "' In The Collection.");
                tmpRes = new TH1Armour();
            }
            return tmpRes;
        }
        public List<TH1Armour> listArmour(char colourKey, int armourType, int characterClass, int alignment)
        {
            List<TH1Armour> tmpRes = new List<TH1Armour>();
            foreach (TH1Armour armo in _collection.Values)
            {
                if (armo.colourKey == colourKey && armo.type == armourType && (characterClass == -1 || armo.charClass == characterClass) && (alignment == -1 || armo.alignment == alignment))
                {
                    tmpRes.Add(armo);
                }
            }
            return tmpRes;
        }
        public List<string> listArmourByName(char colourKey, int armourType, int characterClass, int alignment)
        {
            List<string> tmpRes = new List<string>();
            foreach (TH1Armour armo in _collection.Values)
            {
                if (armo.colourKey == colourKey && armo.type == armourType && (characterClass == -1 || armo.charClass == characterClass) && (alignment == -1 || armo.alignment == alignment))
                {
                    tmpRes.Add(armo.armourLongIdName);
                }
            }
            return tmpRes;
        }
    }
    public class TH1ArmourExt
    {
        // Private
        private TH1Armour _armour;
        private bool _crafted; // always true?
        private uint _valueB; // boolean?
        private uint _condition;
        private bool _isEquipt; // always false?
        private TH1Paint _paint;
        private List<TH1RuneMExt> _runesInserted;

        // Public
        public TH1ArmourExt(TH1Armour armour)
        {
            _armour = armour;
            _paint = _armour.paint;
            _condition = (uint)_armour.condition;
            _crafted = true;
            _isEquipt = false;
            _runesInserted = new List<TH1RuneMExt>();
        }

        public TH1Armour armour
        {
            get
            {
                return _armour;
            }
        }
        public bool crafted
        {
            get
            {
                return _crafted;
            }
            set
            {
                _crafted = value;
            }
        }
        public uint craftedUint
        {
            get
            {
                return _crafted ? 1u : 0u;
            }
            set
            {
                _crafted = (value == 1);
            }
        }
        public uint valueB
        {
            get
            {
                return _valueB;
            }
            set
            {
                _valueB = value;
            }
        }
        public uint condition
        {
            get
            {
                return _condition;
            }
            set
            {
                _condition = value;
            }
        }
        public bool isEquipt
        {
            get
            {
                return _isEquipt;
            }
            set
            {
                _isEquipt = value;
            }
        }
        public uint isEquiptUint
        {
            get
            {
                return _isEquipt ? 1u : 0u;
            }

            set
            {
                _isEquipt = (value == 1u);
            }
        }
        public TH1Paint paint
        {
            get
            {
                return _paint;
            }
            set
            {
                _paint = value;
            }
        }
        public List<TH1RuneMExt> runesInserted
        {
            get
            {
                return _runesInserted;
            }

            set
            {
                _runesInserted = value;
            }
        }
        public List<string> bonusRunes
        {
            get
            {
                return _armour.runes;
            }
        }

        // Additional Publics
        public int armourType
        {
            get
            {
                // return _armour.type;
                return _armour.type;
            }
        }
        public string armourTypeName
        {
            get
            {
                return new TH1Helper().armourTypesDic.Values.ToArray()[_armour.type];
            }
        }
        public string armourName
        {
            get
            {
                return _armour.armourName;
            }
        }
        public string armourLongName
        {
            get
            {
                return _armour.armourLongName;
            }
        }
        public string armourLongIdName
        {
            get
            {
                return _armour.armourLongIdName;
            }
        }
        public int maxCondition
        {
            get
            {
                return _armour.condition;
            }
        }
        public decimal conditionPerc
        {
            get
            {
                return (decimal)condition / (decimal)_armour.condition;
            }
        }
        public char colourKey
        {
            get
            {
                return _armour.colourKey;
            }
        }
        public int alignment
        {
            get
            {
                return _armour.alignment;
            }
        }
        public string alignmentName
        {
            get
            {
                if (_armour.alignment > -1) return new TH1Helper().alignmentNamesArray[_armour.alignment];
                else return "None";
            }
        }
        public int level
        {
            get
            {
                return _armour.level;
            }
        }
        public int charClass
        {
            get
            {
                return _armour.charClass;
            }
        }
        public string charClassName
        {
            get
            {
                return _armour.charClassName;
            }
        }
        public BitmapImage image
        {
            get
            {
                return _armour.image;
            }
        }
        public int emptyRuneSlots
        {
            get
            {
                return _armour.emptyRuneSlots;
            }
        }
        public int freeRuneSlots
        {
            get
            {
                return _armour.emptyRuneSlots - runesInserted.Count;
            }
        }
        public string freeRuneOfEmpty
        {
            get
            {
                if (emptyRuneSlots == 0) return "None";
                else return string.Format("{0}/{1}", freeRuneSlots, emptyRuneSlots);
            }
        }
        public byte[] armourExtToArray
        {
            get
            {
                string tmpName = _armour.armourString;
                int runesArrayLength = 0;
                foreach (TH1RuneMExt tmpRune in _runesInserted) runesArrayLength += tmpRune.runeExtToArray.Length;

                int tmpLength =
                    4 +                 // Name Length
                    tmpName.Length +    // Name
                    1 +                 // Null Byte
                    4 +                 // Count Runes Inserted
                    runesArrayLength +  // Runes Inserted
                    4 +                 // Header
                    (4 * 5);            // Ext Data

                byte[] tmpArmour = new byte[tmpLength];
                RWStream writer = new RWStream(tmpArmour, true, true);
                try
                {
                    // Weapon String
                    writer.WriteUInt32((uint)tmpName.Length + 1);
                    writer.WriteString(tmpName, StringType.Ascii, tmpName.Length);
                    writer.WriteBytes(new byte[] { 0x00 });

                    // Runes Inserted
                    writer.WriteUInt32((uint)_runesInserted.Count);
                    foreach (TH1RuneMExt tmpRune in _runesInserted)
                        writer.WriteBytes(tmpRune.runeExtToArray);

                    // Weapon Ext
                    writer.WriteBytes(new byte[] { 0x12, 0x34, 0x56, 0x78 });
                    writer.WriteUInt32(craftedUint);
                    writer.WriteUInt32(_valueB);
                    writer.WriteUInt32(_condition);
                    writer.WriteUInt32(isEquiptUint);
                    writer.WriteUInt32((uint)paint.paintID);
                }
                catch { }
                finally { writer.Flush(); tmpArmour = writer.ReadAllBytes(); writer.Close(true); }
                return tmpArmour;
            }
        }
        public string classAlignString
        {
            get
            {
                string prefix = "";
                if (alignmentName != "None") prefix = string.Format("{0}", alignmentName);
                else prefix = "Any";
                if (prefix == charClassName) return "Generic";
                else
                    if (charClassName != "Any") return string.Format("{0} {1}", prefix, charClassName);
                else return string.Format("{1} {0}", prefix, charClassName);
            }
        }
    }
    public class TH1ArmourBaseStats
    {
        // Private
        private int _augIndex;
        private string _armourName;
        private int _armourBase;
        private int _armourType;
        TH1Helper _help = new TH1Helper();

        // Public
        public int augIndex
        {
            get
            {
                return _augIndex;
            }
        }
        public string armourName
        {
            get
            {
                return _armourName;
            }
        }
        public int armourBase
        {
            get
            {
                return _armourBase;
            }
        }
        public int armourType
        {
            get
            {
                return _armourType;
            }
        }
        public string armourTypeName
        {
            get
            {
                return _help.armourTypesArray[_armourType];
            }
        }

        // Construction
        public TH1ArmourBaseStats(int augIndex, string armourName, int armourBase, int armourType)
        {
            _augIndex = augIndex;
            _armourName = armourName;
            _armourBase = armourBase;
            _armourType = armourType;
        }
        public TH1ArmourBaseStats()
        {
            _augIndex = 0;
            _armourName = "assault9";
            _armourBase = 200;
            _armourType = 0;
        }
    }
    public class TH1ArmourBaseStatsJson
    {
        [JsonProperty("Armour")] public string armourName { get; set; }
        [JsonProperty("Base")] public int armourBase { get; set; }
        [JsonProperty("Type")] public int armourType { get; set; }
    }
    public class TH1ArmourBaseStatsCollection
    {
        Dictionary<int, TH1ArmourBaseStats> _collection = new Dictionary<int, TH1ArmourBaseStats>();

        public TH1ArmourBaseStatsCollection()
        {
            string json;
            Dictionary<int, TH1ArmourBaseStatsJson> _parseCollection = new Dictionary<int, TH1ArmourBaseStatsJson>();
            try
            {
                Stream libcom = Application.GetResourceStream(new Uri("pack://application:,,,/Supporting Data/ArmourBaseStats.json.gz")).Stream;
                Stream lib = new GZipStream(libcom, CompressionMode.Decompress, false);
                json = new StreamReader(lib).ReadToEnd();
            }
            catch (Exception ex) { json = ""; MessageBox.Show(ex.ToString()); }

            // Create The Dictionary
            _parseCollection = JsonConvert.DeserializeObject<Dictionary<int, TH1ArmourBaseStatsJson>>(json);
            foreach (KeyValuePair<int, TH1ArmourBaseStatsJson> _item in _parseCollection)
            {
                TH1ArmourBaseStatsJson _armourBase = _item.Value;
                TH1ArmourBaseStats _armourStat = new TH1ArmourBaseStats(_item.Key, _armourBase.armourName, _armourBase.armourBase, _armourBase.armourType);
                _collection.Add(_item.Key, _armourStat);
            }

        }

        // Return Bonus
        public TH1ArmourBaseStats findBaseStats(int id)
        {
            _collection.TryGetValue(id, out TH1ArmourBaseStats tmpRes);
            return tmpRes;
        }

        // Other Functions
    }

    #endregion TH1Armour

    public class TH1SaveStructure
    {

        #region Constants

        // Sector Constants
        public const int TH1_SECTOR_HEADER = 0; // Header & Hash
        public const int TH1_SECTOR_CHARACTER = 1; // Character Data
        public const int TH1_SECTOR_SKILLTREE = 2; // Skill/Class Tree?
        public const int TH1_SECTOR_LOCATION = 3; // Area & Co-Ordinates?
        public const int TH1_SECTOR_CHARM_ACTIVE = 4; // Equipt Charms? - 2x Only, NOID = No Rune
        public const int TH1_SECTOR_RUNE = 5; // Rune Inventry
        public const int TH1_SECTOR_CHARMS_AVAILABLE = 6; // Charms Inventry -- uint #of quests, string size, name, uint 0x00, 0x123456, data length 0x4C, back to string size 
        public const int TH1_SECTOR_WEAPONS = 7; // Weapons -- uint #of weapons, string size, name, uint 0x00, 0x123456, data length 0x14, back to string size
        public const int TH1_SECTOR_ARMOUR = 8; // Armour -- uint #of Armour, 
        public const int TH1_SECTOR_UNKNOWN01 = 9;
        public const int TH1_SECTOR_UNKNOWN02 = 10;
        public const int TH1_SECTOR_CHARMS_INCOMPLETE = 11; // ?
        public const int TH1_SECTOR_WEAPON_BLUEPRINTS = 12;
        public const int TH1_SECTOR_ARMOUR_BLUEPRINTS = 13;

        // Save Types
        private const int TH1_SAVETYPE_NONE = 00;
        private const int TH1_SAVETYPE_CON = 01;
        private const int TH1_SAVETYPE_RAW = 02;

        // Fixed Offsets (well, ya body's no good without a skeleton!)
        private const long TH1_OFFSET_HASH = 0x10;
        private const long TH1_OFFSET_SIZE = 0x24;

        // Limits (for control)
        private const long TH1_LIMIT_MINSIZE = 0x0798;

        // Moi
        private static readonly byte[] PUBLIC_FOOTER = new byte[]
        {
            0x78, 0x4A, 0x61, 0x6D, 0x2E, 0x65, 0x73, 0x2F,
            0x54, 0x6F, 0x6F, 0x48, 0x75, 0x6D, 0x61, 0x6E,
            0x20, 0x2D, 0x20, 0x54, 0x6F, 0x6F, 0x20, 0x48,
            0x75, 0x6D, 0x61, 0x6E, 0x20, 0x31, 0x20, 0x53,
            0x61, 0x76, 0x65, 0x20, 0x45, 0x64, 0x69, 0x74,
            0x6F, 0x72
        };

        //  Bauldur's Secret
        private static readonly byte[] PRIVATE_KEY = new byte[]
        {
            0x3E, 0x4B, 0x34, 0xDF, 0xB7, 0x76, 0x7B, 0x71,
            0x85, 0x95, 0x40, 0x52, 0x74, 0xAF, 0xB2, 0x65
        };

        // Valid Save Header
        private static readonly byte[] PUBLIC_HEADER = new byte[]
        {
            0x12, 0x34, 0x56, 0x78, 0x00, 0x00, 0x00, 0x30,
            0x54, 0x48, 0x31, 0x00, 0x00, 0x00, 0x00, 0x42
        };

        // Sector Only Header
        private static readonly byte[] SECTOR_HEADER = new byte[]
        {
            0x12, 0x34, 0x56, 0x78
        };

        //  Is this the chicken or the egg?
        private static readonly byte[] PLACEHOLDER_HASH = new byte[]
        {
            0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        // The beginning of time ..
        static readonly DateTime Epoch = new DateTime(
            1970, 1, 1, 0, 0, 0,
            DateTimeKind.Utc
        );

        #endregion Constants

        #region Variables

        // General Variables
        public string filePath;
        public int saveType;
        public byte[] rawData;
        public byte[] hash;
        public Boolean hashVerified;
        public long dataSize
        {
            get
            {
                long tmpres = 0;
                foreach (TH1Sector _sector in this.sectors)
                    tmpres += 4 + _sector.size;
                return tmpres;
            }
        }

        // Gamesave Parsing
        public List<TH1Sector> sectors;
        public TH1Character character;
        public List<TH1RuneMExt> runes;
        public TH1SkillsTree skills;
        public TH1CharmExt[] charmsActive;
        public List<TH1CharmExt> charmsInventry;
        public List<TH1Obelisk> charmsActiveEx;
        public List<TH1WeaponExt> weaponsInventory;
        public List<TH1WeaponExt> weaponsBlueprints;
        public List<TH1ArmourExt> armourInventory;
        public List<TH1ArmourExt> armourBlueprints;

        // Problems
        public long lastError;       // current 0
        public string lastErrorMsg;

        // Collections
        public TH1Collections db;

        #endregion Variables

        #region Init (Reset)
        private void Init()
        {
            // General
            this.filePath = "";
            this.saveType = TH1_SAVETYPE_NONE;
            this.rawData = null;
            this.hash = PLACEHOLDER_HASH;
            this.hashVerified = false;

            // Parsed
            this.sectors = new List<TH1Sector>();
            this.character = new TH1Character();
            this.runes = new List<TH1RuneMExt>();
            this.skills = new TH1SkillsTree(0);
            this.charmsActive = new TH1CharmExt[2];
            this.charmsInventry = new List<TH1CharmExt>();
            this.charmsActiveEx = new List<TH1Obelisk>();
            this.weaponsInventory = new List<TH1WeaponExt>();
            this.weaponsBlueprints = new List<TH1WeaponExt>();
            this.armourInventory = new List<TH1ArmourExt>();
            this.armourBlueprints = new List<TH1ArmourExt>();

            // Databases
            if (this.db == null) this.db = new TH1Collections();

            // Checks
            clearError();
        }

        #endregion Init (Reset)

        #region Public

        public void readSaveFile(string filePath)
        {
            // Try The File
            Init();
            this.filePath = filePath;
            readTH1Gamesave();

            // If Sucessful
            if (this.lastError == 0)
            {
                // Load Settings
                loadGamesaveSectors();
                dataToCharacter();
                dataToSkillsTree();
                dataToRunes();
                dataToCharmsActive();
                dataToCharmsInventry();
                dataToWeaponsInventory();
                dataToWeaponsBlueprints();
                dataToArmourInventory();
                dataToArmourBlueprints();
            }

        }

        public void writeSaveFile(string outFilePath)
        {
            byte[] gamesaveOut;

            // Save Settings
            characterToData();
            skillsTreeToData();
            runesToData();
            charmsActiveToData();
            charmsInventryToData();
            weaponsInventoryToData();
            weaponsBlueprintsToData();
            armourBlueprintsToData();
            // Under Development
            // armourInventoryToData();

            // Create the Save Buffer in Memory
            gamesaveOut = rebuildSave();
            long saveLength = gamesaveOut.Length;

            Array.Resize(ref gamesaveOut, (int)(saveLength + PUBLIC_FOOTER.Length));
            Array.Copy(PUBLIC_FOOTER, 0, gamesaveOut, saveLength, PUBLIC_FOOTER.Length);

            try
            {
                using (var fs = new FileStream(outFilePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(gamesaveOut, 0, gamesaveOut.Length);
                }
            }
            catch (Exception ex)
            {
                setError(7, "Unable To Write Gamesave: " + ex.ToString());
            }

        }

        #endregion Public

        #region General IO Functions

        private void readTH1Gamesave()
        {
            if (File.Exists(this.filePath))
            {
                // Detect File format
                saveType = readSaveType(this.filePath);

                // Do Something With It
                switch (this.saveType)
                {
                    case TH1_SAVETYPE_CON:
                        loadConFromFile();
                        break;
                    case TH1_SAVETYPE_RAW:
                        loadRawFromFile();
                        break;
                    default:
                        setError(6, "Save Type Not Recognised");
                        break;
                }

                // Do Some Further Basic Corruption Checks
                verifyRawData();

            }
            // return rawsave;
        }

        // Simple - RawData Loader
        private void loadRawFromFile()
        {
            RWStream reader = new RWStream(filePath, true);
            try
            {
                this.rawData = reader.ReadAllBytes();
            }
            catch (Exception ex) { MsgBox(ex.ToString()); }
            finally { reader.Close(false); }
        }

        // Simple (with the right library) - CON Loader
        private void loadConFromFile()
        {
            Stfs confs;
            confs = new Stfs(this.filePath);
            if (confs.HeaderData.SignatureHeaderType != SignatureType.Con ||
            !confs.HeaderData.TitleID.Contains("4D5307DE"))
            {
                confs.Close();
            } else
            {
                this.rawData = confs.Extract(0);
            }
            confs.Close();
        }

        private int readSaveType(string filepath)
        {
            int tmpres = TH1_SAVETYPE_NONE;
            int peekSize = 4;
            byte[] bytesbuff;
            var byteCon = new byte[] { 0x43, 0x4F, 0x4E, 0x20 };
            var byteRaw = new byte[peekSize];

            Array.Copy(PUBLIC_HEADER, byteRaw, peekSize);
            try
            {
                RWStream reader = new RWStream(filePath, true);
                try
                {
                    bytesbuff = reader.PeekBytes(peekSize);
                    if (bytesbuff.SequenceEqual(byteCon)) tmpres = TH1_SAVETYPE_CON;
                    if (bytesbuff.SequenceEqual(byteRaw)) tmpres = TH1_SAVETYPE_RAW;
                }
                catch (Exception ex) { MsgBox(ex.ToString()); }
                finally { reader.Close(false); }
            }
            catch { }

            return tmpres;
        }

        private byte[] abra(byte[] header)
        {
            // Abra used Teleport
            var tmpres = new byte[header.Length];
            byte[] output;

            Array.Copy(header, tmpres, header.Length);
            for (int i = 0; i < tmpres.Length; i++) {
                tmpres[i] ^= PRIVATE_KEY[i];
                tmpres[i] ^= PUBLIC_FOOTER[i];
            }
            output = Encoding.ASCII.GetBytes(BitConverter.ToString(tmpres).Replace("-", ""));
            return output;
        }

        private byte[] rewriteFileSize(byte[] data, uint newSize)
        {
            long tmpOffset = TH1_OFFSET_SIZE;
            RWStream writer = new RWStream(data, true, true);
            try
            {
                writer.Position = tmpOffset;
                writer.WriteUInt32(newSize);
            }
            catch (Exception ex) { setError(11, "Unable To Write New Filesize: " + ex.ToString()); }
            finally { data = writer.ReadAllBytes(); writer.Close(true); }
            return data;
        }

        #endregion General IO Functions

        #region Sector IO
        private void rebuildSector(int sectorID, byte[] newData)
        {
            // this should be enough (for now)
            foreach (TH1Sector _sector in this.sectors)
                if (_sector.id == sectorID) _sector.data = newData;
        }

        private byte[] getSectorData(int sectorID, bool isDebug)
        {
            byte[] tmpRes = new byte[0];
            foreach (TH1Sector _sector in this.sectors)
                if (_sector.id == sectorID)
                    if (isDebug) return debugStripSector(sectorID, _sector.data);
                    else return _sector.data;
            return tmpRes;
        }

        // Overload
        private byte[] getSectorData(int sectorID)
        {
            return getSectorData(sectorID, false);
        }

        private byte[] rebuildSave()
        {
            // Get Full Size
            long fullSize = 0;
            for (int i = 0; i < this.sectors.Count; i++)
                fullSize += SECTOR_HEADER.Length + this.sectors[i].size;

            // Reserve Memory
            byte[] tmpRes = new byte[fullSize];
            long pointer = 0;

            // Copy Data
            for (int i = 0; i < this.sectors.Count; i++)
            {
                // Header
                Array.Copy(SECTOR_HEADER, 0, tmpRes, pointer, SECTOR_HEADER.Length);
                pointer += 4;
                // Data
                Array.Copy(this.sectors[i].data, 0, tmpRes, pointer, this.sectors[i].size);
                pointer += this.sectors[i].size;
            }

            // Write New File Size
            tmpRes = rewriteFileSize(tmpRes, (uint)fullSize);

            // Hash The File
            tmpRes = rewriteHash(tmpRes);

            return tmpRes;
        }

        // Dumping Sectors..
        public void writeSectorToFile(string filename, int sectorID, bool incHeader, bool isDebug )
        {

            // Write All Sectors
            if ((this.lastError == 0) && (sectorID <= this.sectors.Count) && (sectorID >= 0))
            {
                // Grab Data
                byte[] sectordata = getSectorData(sectorID, isDebug);

                // Write It
                RWStream writer = new RWStream(File.Open(filename, FileMode.Create), true);
                if (incHeader)
                {
                    writer.WriteBytes(SECTOR_HEADER);
                }
                writer.WriteBytes(sectordata);
                writer.Flush();
                writer.Close(false);
            }
        }

        private byte[] debugStripSector( int sectorID, byte[] sectorData )
        {
            byte[] tmpRes = sectorData;
            switch(sectorID)
            {
                case TH1_SECTOR_CHARACTER:
                    tmpRes = debugCharacterToData();
                    break;
            }
            return tmpRes;
        }

        #endregion Sector IO

        #region Character IO
        private void dataToCharacter()
        {
            // Supports
            // - Name
            // - Alignment
            // - Class
            // - Level
            // - Exp
            // - Bounty
            // - Skillpoints
            // - SaveSlot
            // - Last Saved
            // - Data Pairs A
            // - Data Pairs B

            // temp load the character data
            byte[] charData = getSectorData(TH1_SECTOR_CHARACTER);
            TH1Character tmpChar = this.character;

            RWStream reader = new RWStream(charData, true);
            try
            {

                // Character Name
                reader.Position = tmpChar.OFFSET_NAME_A_LENGTH;
                int namelength = (int)reader.PeekUInt32();
                reader.Position = tmpChar.OFFSET_NAME_A;
                tmpChar.name = reader.ReadString(StringType.Ascii, namelength - 1);

                tmpChar.OFFSET_ALIGNMENT = tmpChar.OFFSET_NAME_A + namelength + (13 * 4);
                reader.Position = tmpChar.OFFSET_ALIGNMENT;
                tmpChar.alignment = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_CLASS;
                tmpChar.charClass = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_LEVELA;
                tmpChar.level = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_EXP;
                tmpChar.exp = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_BOUNTY;
                tmpChar.bounty = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_SKILLPOINTSA;
                tmpChar.skillPoints = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_SAVESLOT;
                tmpChar.saveSlot = reader.ReadUInt32();

                // Last Saved
                reader.Position = tmpChar.OFFSET_LAST_SAVED;
                tmpChar.lastSave = DateTime.FromFileTimeUtc(reader.ReadInt64()); 

                reader.Position = tmpChar.OFFSET_DATA_PAIRSA;
                for (int i = 0; i < tmpChar.LIMIT_DATA_PAIRSA; i++)
                {
                    tmpChar.dataPairsA.Add(lookupDataPairName(reader.ReadUInt32(), 1), reader.ReadUInt32());
                }

                reader.Position = tmpChar.OFFSET_DATA_PAIRSB;
                for (int i = 0; i < tmpChar.LIMIT_DATA_PARISB; i++)
                {
                    tmpChar.dataPairsB.Add(lookupDataPairName(reader.ReadUInt32(), 2), reader.ReadSingle());
                }

                //tmpChar.playtime;

            }
            catch (Exception ex) { MsgBox(ex.ToString()); }
            finally { reader.Close(false); this.character = tmpChar; }
        }

        private void characterToData()
        {

            // Supports
            // - Name (Unicode & ASCII)
            // - Alignment
            // - Class
            // - Level (x2 Offsets)
            // - Exp
            // - Bounty
            // - Skillpoints
            // - SaveSlot
            // - Last Saved
            // - Data Pairs A
            // - Data Pairs B

            // temp load the character data
            byte[] charDataTemp = new byte[1];

            byte[] charData = getSectorData(TH1_SECTOR_CHARACTER);
            TH1Character tmpChar = this.character;

            // No Overflows
            tmpChar.name = tmpChar.name.Substring(0, Math.Min(tmpChar.name.Length, tmpChar.LIMIT_NAME_LENGTH));

            RWStream namewriter = new RWStream(charData, true, true);
            try
            {
                // Character Name
                namewriter.Position = tmpChar.OFFSET_NAME_A_LENGTH;
                uint oldNameLength = namewriter.ReadUInt32() - 1;
                uint newNameLength = (uint)tmpChar.name.Length;

                namewriter.Position = tmpChar.OFFSET_NAME_A_LENGTH;
                namewriter.WriteUInt32(newNameLength + 1);

                tmpChar.OFFSET_ALIGNMENT = tmpChar.OFFSET_NAME_A + (newNameLength + 1) + (13 * 4);

                charDataTemp = new byte[charData.Length - oldNameLength + newNameLength];

                // i think ?!
                Array.Copy(charData, charDataTemp, tmpChar.OFFSET_NAME_A);
                Array.Copy(Encoding.ASCII.GetBytes(tmpChar.name), 0, charDataTemp, tmpChar.OFFSET_NAME_A, tmpChar.name.Length);
                Array.Copy(charData, tmpChar.OFFSET_NAME_A + oldNameLength, charDataTemp, tmpChar.OFFSET_NAME_A + newNameLength, charData.Length - tmpChar.OFFSET_NAME_A - oldNameLength);
            }
            catch (Exception ex) { setError(9, "Failed To Write Character Name To Buffer: " + ex.ToString()); return; }

            charData = charDataTemp;

            RWStream writer = new RWStream(charData, true, true);
            try
            {
                TH1ExpToNextLevel _expCalc = new TH1ExpToNextLevel();

                // Write Unicode Name
                writer.Position = tmpChar.OFFSET_NAME_U;
                writer.WriteString(tmpChar.name, StringType.Unicode, tmpChar.name.Length);
                for (int uniz = tmpChar.name.Length; uniz < tmpChar.LIMIT_NAME_LENGTH; uniz++) writer.WriteBytes(new byte[] { 0x00, 0x00 });

                // Write Alignment
                writer.Position = tmpChar.OFFSET_ALIGNMENT;
                writer.WriteUInt32((uint)tmpChar.alignment);

                // Write Class
                writer.Position = tmpChar.OFFSET_CLASS;
                writer.WriteUInt32((uint)tmpChar.charClass);

                // Write Level
                writer.Position = tmpChar.OFFSET_LEVELA;
                writer.WriteUInt32((uint)tmpChar.level);

                writer.Position = tmpChar.OFFSET_LEVELB;
                writer.WriteUInt32((uint)tmpChar.level);

                // Write EXP
                writer.Position = tmpChar.OFFSET_EXP;
                writer.WriteUInt32((uint)tmpChar.exp);

                writer.Position = tmpChar.OFFSET_CURR_LEVEL_EXP;
                writer.WriteUInt32((uint)_expCalc.expProgressToNext(tmpChar.exp));

                // Write Bounty
                writer.Position = tmpChar.OFFSET_BOUNTY;
                writer.WriteUInt32((uint)tmpChar.bounty);

                // Write Skillpoints
                writer.Position = tmpChar.OFFSET_SKILLPOINTSA;
                writer.WriteUInt32((uint)tmpChar.skillPoints);
                writer.Position = tmpChar.OFFSET_SKILLPOINTSB;
                writer.WriteUInt32((uint)tmpChar.skillPoints);

                // Write Save Slot
                writer.Position = tmpChar.OFFSET_SAVESLOT;
                writer.WriteUInt32((uint)tmpChar.saveSlot);

                // Update Last Saved
                writer.Position = tmpChar.OFFSET_LAST_SAVED;
                long roundingSeconds = 10000000;
                writer.WriteInt64(tmpChar.lastSave.ToFileTimeUtc() / roundingSeconds * 10000000);

                writer.Position = tmpChar.OFFSET_DATA_PAIRSA;
                foreach (KeyValuePair<string, uint> dp in tmpChar.dataPairsA)
                {
                    writer.WriteUInt32(lookupDataPairValue(dp.Key, 1));
                    writer.WriteUInt32(dp.Value);
                }

                writer.Position = tmpChar.OFFSET_DATA_PAIRSB;
                foreach (KeyValuePair<string, float> dp in tmpChar.dataPairsB)
                {
                    writer.WriteUInt32(lookupDataPairValue(dp.Key, 2));
                    writer.WriteSingle(dp.Value);
                }

            }
            catch (Exception ex) { setError(10, "Unable To Write Character Stats To Save Data: " + ex.ToString()); return; }
            finally { writer.Flush(); charData = writer.ReadAllBytes(); writer.Close(false); }

            rebuildSector(TH1_SECTOR_CHARACTER, charData);

        }

        private byte[] debugCharacterToData()
        {

            // temp load the character data
            byte[] charDataTemp = new byte[1];

            byte[] charData = getSectorData(TH1_SECTOR_CHARACTER);
            TH1Character tmpChar = this.character;

            // No Overflows
            tmpChar.name = "DEBUG";

            RWStream namewriter = new RWStream(charData, true, true);
            try
            {
                // Character Name
                namewriter.Position = tmpChar.OFFSET_NAME_A_LENGTH;
                uint oldNameLength = namewriter.ReadUInt32() - 1;
                uint newNameLength = (uint)tmpChar.name.Length;
                namewriter.Position = tmpChar.OFFSET_NAME_A_LENGTH;
                namewriter.WriteUInt32(newNameLength + 1);
                tmpChar.OFFSET_ALIGNMENT = tmpChar.OFFSET_NAME_A + (newNameLength + 1) + (13 * 4);
                charDataTemp = new byte[charData.Length - oldNameLength + newNameLength];
                Array.Copy(charData, charDataTemp, tmpChar.OFFSET_NAME_A);
                Array.Copy(Encoding.ASCII.GetBytes(tmpChar.name), 0, charDataTemp, tmpChar.OFFSET_NAME_A, tmpChar.name.Length);
                Array.Copy(charData, tmpChar.OFFSET_NAME_A + oldNameLength, charDataTemp, tmpChar.OFFSET_NAME_A + newNameLength, charData.Length - tmpChar.OFFSET_NAME_A - oldNameLength);
            }
            catch (Exception ex) { setError(9, "Failed To Write Character Name To Buffer: " + ex.ToString()); return new byte[0]; }

            charData = charDataTemp;

            RWStream writer = new RWStream(charData, true, true);
            try
            {
                TH1ExpToNextLevel _expCalc = new TH1ExpToNextLevel();

                // Write Unicode Name
                writer.Position = tmpChar.OFFSET_NAME_U;
                writer.WriteString(tmpChar.name, StringType.Unicode, tmpChar.name.Length);
                for (int uniz = tmpChar.name.Length; uniz < tmpChar.LIMIT_NAME_LENGTH; uniz++) writer.WriteBytes(new byte[] { 0x00, 0x00 });

                // Write Alignment
                writer.Position = tmpChar.OFFSET_ALIGNMENT;
                writer.WriteUInt32(0);

                // Write Class
                writer.Position = tmpChar.OFFSET_CLASS;
                writer.WriteUInt32(0);

                // Write Level
                writer.Position = tmpChar.OFFSET_LEVELA;
                writer.WriteUInt32(0);

                writer.Position = tmpChar.OFFSET_LEVELB;
                writer.WriteUInt32(0);

                // Write EXP
                writer.Position = tmpChar.OFFSET_EXP;
                writer.WriteUInt32(0);

                writer.Position = tmpChar.OFFSET_CURR_LEVEL_EXP;
                writer.WriteUInt32(0);

                // Write Bounty
                writer.Position = tmpChar.OFFSET_BOUNTY;
                writer.WriteUInt32(0);

                // Write Skillpoints
                writer.Position = tmpChar.OFFSET_SKILLPOINTSA;
                writer.WriteUInt32(0);
                writer.Position = tmpChar.OFFSET_SKILLPOINTSB;
                writer.WriteUInt32(0);

                // Write Save Slot
                writer.Position = tmpChar.OFFSET_SAVESLOT;
                writer.WriteUInt32(0);

                // Update Last Saved
                writer.Position = tmpChar.OFFSET_LAST_SAVED;
                writer.WriteInt64(0);

                writer.Position = tmpChar.OFFSET_DATA_PAIRSA;
                foreach (KeyValuePair<string, uint> dp in tmpChar.dataPairsA)
                {
                    writer.WriteUInt32(lookupDataPairValue(dp.Key, 1));
                    if (dp.Key.Length > 2) writer.WriteUInt32(0);
                    else writer.WriteUInt32(dp.Value);
                }

                writer.Position = tmpChar.OFFSET_DATA_PAIRSB;
                foreach (KeyValuePair<string, float> dp in tmpChar.dataPairsB)
                {
                    writer.WriteUInt32(lookupDataPairValue(dp.Key, 2));
                    if (dp.Key.Length > 2) writer.WriteSingle(0);
                    else writer.WriteSingle(dp.Value);
                }

            }
            catch (Exception ex) { setError(10, "Unable To Write Character Stats To Save Data: " + ex.ToString()); return new byte[0]; }
            finally { writer.Flush(); charData = writer.ReadAllBytes(); writer.Close(false); }

            return charData;
        }

        #endregion Character IO

        #region Skills Tree IO

        private void dataToSkillsTree()
        {
            // Reset With Default Class
            TH1SkillsTree tmpSkills = new TH1SkillsTree(0);

            // Buffer It
            byte[] skillsData = getSectorData(TH1_SECTOR_SKILLTREE);

            RWStream reader = new RWStream(skillsData, true);
            try
            {
                // Read In Character Class
                tmpSkills = new TH1SkillsTree(reader.ReadUInt32()); // New With Alignment

                while (reader.Position < reader.Length)
                {
                    TH1SkillsTreePair stp = new TH1SkillsTreePair();
                    stp.first = reader.ReadUInt32();
                    stp.second = reader.ReadUInt32();
                    tmpSkills.pairs.Add(stp);
                }

            }
            catch (Exception ex) { setError(12, "Unable To Parse Skills Tree: " + ex.ToString()); }
            finally { reader.Close(false); this.skills = tmpSkills; }
        }

        private void skillsTreeToData()
        {
            // Buffer It
            byte[] skillsData = new byte[(this.skills.pairs.Count * 8) + 4];

            RWStream writer = new RWStream(skillsData, true, true);
            try
            {
                writer.WriteUInt32((uint)this.character.charClass);

                for (int num = 0; num < this.skills.pairs.Count; num++)
                {
                    writer.WriteUInt32((uint)this.skills.pairs[num].first);
                    writer.WriteUInt32((uint)this.skills.pairs[num].second);
                }
            }
            catch (Exception ex) { setError(13, "Unable To Write Skills Tree: " + ex.ToString()); }
            finally { writer.Flush(); skillsData = writer.ReadAllBytes(); writer.Close(false); }

            // replaceSector(TH1_SECTOR_SKILLTREE, skillsData);
            rebuildSector(TH1_SECTOR_SKILLTREE, skillsData);
        }

        #endregion Skills Tree IO

        #region Runes IO

        private void dataToRunes()
        {
            List<TH1RuneMExt> tmpRunes = new List<TH1RuneMExt>();

            // Buffer It
            byte[] runesData = getSectorData(TH1_SECTOR_RUNE);

            RWStream reader = new RWStream(runesData, true);
            try
            {
                int runeCount = (int)reader.ReadUInt32();

                for (int runeloop = 0; runeloop < runeCount; runeloop++)
                {
                    int nameLength = (int)reader.ReadUInt32();
                    byte[] runeName = reader.ReadBytes(nameLength);

                    TH1RuneMExt tmpRune = new TH1RuneMExt(db.runeMCollection.findRune(runeName));
                    if (tmpRune.rune == null) tmpRune = new TH1RuneMExt(new TH1RuneM());

                    tmpRune.purchased = reader.ReadUInt32();
                    tmpRune.dataB = reader.ReadUInt32();
                    tmpRune.valueModifier = reader.ReadUInt32();
                    tmpRune.dataD = reader.ReadUInt32();
                    tmpRune.paint = db.paintCollection.findPaint((int)reader.ReadUInt32());

                    tmpRunes.Add(tmpRune);
                }

            }
            catch (Exception ex) { setError(14, "Unable To Parse Runes: " + ex.ToString()); }
            finally { reader.Close(false); this.runes = tmpRunes; }

        }

        private void runesToData()
        {
            long bytesize = 0;
            foreach (TH1RuneMExt tmpRune in this.runes) bytesize += tmpRune.runeExtToArray.Length;
            byte[] tmpRunes = new byte[4 + bytesize];

            RWStream writer = new RWStream(tmpRunes, true, true);
            try
            {
                writer.WriteUInt32((uint)this.runes.Count);
                foreach (TH1RuneMExt runeLoop in this.runes) writer.WriteBytes(runeLoop.runeExtToArray);
            } catch (Exception ex) { setError(15, "Unable To Write Runes: " + ex.ToString()); }
            finally { writer.Flush(); tmpRunes = writer.ReadAllBytes(); writer.Close(true); }

            // replaceSector(TH1_SECTOR_RUNE, tmpRunes);
            rebuildSector(TH1_SECTOR_RUNE, tmpRunes);
        }

        #endregion Runes IO

        #region Charms / Obelisks IO
        private void dataToCharmsActive()
        {
            // Parse It
            dataToCharms(getSectorData(TH1_SECTOR_CHARM_ACTIVE), true);
        }

        private void dataToCharmsInventry()
        {
            // Parse It
            dataToCharms(getSectorData(TH1_SECTOR_CHARMS_AVAILABLE), false);
            dataToCharms(getSectorData(TH1_SECTOR_CHARMS_INCOMPLETE), false);
        }

        private void charmsActiveToData()
        {
            long bytesize = 0;
            foreach (TH1CharmExt _charm in this.charmsActive) bytesize += _charm.charmToActiveArray.Length;
            byte[] tmpCharms = new byte[bytesize + 4 + (this.charmsActiveEx.Count * 8)];

            RWStream writer = new RWStream(tmpCharms, true, true);
            try
            {
                foreach (TH1CharmExt _charmLoop in this.charmsActive) writer.WriteBytes(_charmLoop.charmToActiveArray);
                writer.WriteUInt32((uint)this.charmsActiveEx.Count);
                foreach (TH1Obelisk _exLoop in this.charmsActiveEx)
                {
                    writer.WriteUInt32(_exLoop.key);
                    writer.WriteUInt32(_exLoop.valueUint);
                }
            }
            catch (Exception ex) { setError(17, "Unable To Write Active Charms: " + ex.ToString()); }
            finally { writer.Flush(); tmpCharms = writer.ReadAllBytes(); writer.Close(true); }

            // replaceSector(TH1_SECTOR_CHARM_ACTIVE, tmpCharms);
            rebuildSector(TH1_SECTOR_CHARM_ACTIVE, tmpCharms);
        }

        private void charmsInventryToData()
        {
            // MessageBox.Show("charmsInventryToData()");
            List<TH1CharmExt> _complete = new List<TH1CharmExt>();
            List<TH1CharmExt> _incomplete = new List<TH1CharmExt>();

            long _completeSize = 0;
            long _incompleteSize = 0;

            // Getting Organised
            foreach (TH1CharmExt _charm in this.charmsInventry)
            {
                if (_charm.isComplete)
                {
                    _complete.Add(_charm);
                    _completeSize += _charm.charmToInventryArray.Length;
                }
                else
                {
                    _incomplete.Add(_charm);
                    _incompleteSize += _charm.charmToInventryArray.Length;
                }
            }

            // Buffering
            byte[] _completeData = new byte[4 + _completeSize];
            byte[] _incompleteData = new byte[4 + _incompleteSize];

            // Write Complete
            RWStream _completeWriter = new RWStream(_completeData, true, true);
            try
            {
                _completeWriter.WriteUInt32((uint)_complete.Count);
                foreach (TH1CharmExt _charm in _complete)
                {
                    _completeWriter.WriteBytes(_charm.charmToInventryArray);
                }
            }
            catch (Exception ex) { setError(18, "Unable To Write Complete Charms: " + ex.ToString()); }
            finally { _completeWriter.Flush(); _completeData = _completeWriter.ReadAllBytes(); _completeWriter.Close(true); }

            // Write Incomplete
            RWStream _incompleteWriter = new RWStream(_incompleteData, true, true);
            try
            {
                _incompleteWriter.WriteUInt32((uint)_incomplete.Count);
                foreach (TH1CharmExt _charm in _incomplete)
                {
                    _incompleteWriter.WriteBytes(_charm.charmToInventryArray);
                }
            }
            catch (Exception ex) { setError(19, "Unable To Write Incomplete Charms: " + ex.ToString()); }
            finally { _incompleteWriter.Flush(); _incompleteData = _incompleteWriter.ReadAllBytes(); _incompleteWriter.Close(true); }

            // Replace Sectors
            // replaceSector(TH1_SECTOR_CHARMS_AVAILABLE, _completeData);
            // replaceSector(TH1_SECTOR_CHARMS_INCOMPLETE, _incompleteData);
            rebuildSector(TH1_SECTOR_CHARMS_AVAILABLE, _completeData);
            rebuildSector(TH1_SECTOR_CHARMS_INCOMPLETE, _incompleteData);

        }

        private void dataToCharms(byte[] buffer, bool isactive)
        {
            List<TH1CharmExt> tmpCharms = new List<TH1CharmExt>();
            // If Active Data
            List<TH1Obelisk> tmpExs = new List<TH1Obelisk>();

            RWStream reader = new RWStream(buffer, true);
            try
            {
                uint totalCharms; // Charm Count
                if (isactive) totalCharms = 2;
                else totalCharms = reader.ReadUInt32();

                for (int charmloop = 0; charmloop < totalCharms; charmloop++)
                {
                    int nameLength = (int)reader.ReadUInt32();
                    byte[] runeName = reader.ReadBytes(nameLength);

                    TH1CharmExt tmpCharm = new TH1CharmExt(db.charmCollection.findCharm(runeName));

                    if (!isactive) reader.ReadBytes(8); // Skip The Separator

                    if (tmpCharm.charm != null)
                    {
                        reader.ReadUInt32();    // alwaysTrue
                        tmpCharm.val2 = reader.ReadUInt32();
                        tmpCharm.valueModifier = reader.ReadUInt32();
                        tmpCharm.inActiveSlot = reader.ReadUInt32() == 1; // hm?
                        reader.ReadUInt32();    // alwaysTrue
                        reader.ReadUInt32();    // inActiveSlot 
                        reader.ReadUInt32();    // goalComplete
                        tmpCharm.val8 = reader.ReadUInt32();
                        tmpCharm.progress = reader.ReadUInt32();
                        tmpCharm.activeQuestId = reader.ReadUInt32();
                        uint runeChecks = reader.ReadUInt32();
                        for (int i = 0; i < runeChecks; i++)
                        {
                            tmpCharm.runesReq.Add(reader.ReadUInt32() == 1);
                        }
                    }
                    tmpCharms.Add(tmpCharm);
                }

                if (isactive) // Active Sector Only (Alternate Use?)
                {
                    uint exPairs = reader.ReadUInt32();
                    for (int exLoop = 0; exLoop < exPairs; exLoop++)
                    {
                        TH1Obelisk tmpEx = new TH1Obelisk(reader.ReadUInt32(), reader.ReadUInt32());
                        tmpExs.Add(tmpEx);
                    }
                }

            }
            catch (Exception ex) { setError(16, "Unable To Parse Charms: " + ex.ToString()); }
            finally {
                reader.Close(false);
                if (isactive)
                {
                    this.charmsActive = tmpCharms.ToArray();
                    this.charmsActiveEx = tmpExs;
                } else foreach (TH1CharmExt _charm in tmpCharms) this.charmsInventry.Add(_charm);
            }

        }
        #endregion Charms / Obelisks IO

        #region Weapons IO

        private void dataToWeaponsInventory()
        {
            dataToWeapons(getSectorData(TH1_SECTOR_WEAPONS), false);
        }

        private void dataToWeaponsBlueprints()
        {
            dataToWeapons(getSectorData(TH1_SECTOR_WEAPON_BLUEPRINTS), true);
        }

        private void dataToWeapons( byte[] sector, bool isBlueprint )
        {
            // Setup
            List<TH1WeaponExt> tmpWeapons = new List<TH1WeaponExt>();
            TH1Helper helper = db.helper;
            string[] weaponTypes = helper.weaponTypesDic.Values.ToArray();
            
            // Buffer
            RWStream reader = new RWStream(sector, true);
            try
            {
                // Parse By Type
                for( int i = 0; i < weaponTypes.Length; i++)
                {
                    uint weapCount = reader.ReadUInt32();
                    for( int weapNum = 0; weapNum < weapCount; weapNum ++)
                    {
                        // Weapon Name
                        uint stringLength = reader.ReadUInt32();
                        byte[] nullString = reader.ReadBytes((int)stringLength);
                        TH1WeaponExt thisWeapon = new TH1WeaponExt(db.weaponCollection.findWeapon(nullString));

                        // Inserted Runes
                        List<TH1RuneMExt> runesInserted = new List<TH1RuneMExt>();
                        uint runesInsertedCount = reader.ReadUInt32();
                        for( int runeNum = 0; runeNum < runesInsertedCount; runeNum++)
                        {
                            uint runeStringLength = reader.ReadUInt32();
                            byte[] runeNullString = reader.ReadBytes((int)runeStringLength);
                            TH1RuneMExt tmpRune = new TH1RuneMExt(db.runeMCollection.findRune(runeNullString));

                            tmpRune.purchased = reader.ReadUInt32();
                            tmpRune.dataB = reader.ReadUInt32();
                            tmpRune.valueModifier = reader.ReadUInt32();
                            tmpRune.dataD = reader.ReadUInt32();
                            tmpRune.paint = db.paintCollection.findPaint((int)reader.ReadUInt32());

                            runesInserted.Add(tmpRune);
                        }

                        // Weapon Ext
                        byte[] header = reader.ReadBytes(4); // header (meh)
                        thisWeapon.craftedUint = reader.ReadUInt32();
                        thisWeapon.valueB = reader.ReadUInt32();
                        thisWeapon.condition = reader.ReadUInt32();
                        thisWeapon.isEquiptUint = reader.ReadUInt32();
                        thisWeapon.paint = db.paintCollection.findPaint((int)reader.ReadUInt32());
                        thisWeapon.runesInserted = runesInserted;

                        // Add To Collection
                        tmpWeapons.Add(thisWeapon);
                    }

                    if (reader.Position < reader.Length) reader.ReadBytes(4);
                }
            }
            catch (Exception ex) {
                if( isBlueprint ) setError(20, "Unable To Parse Weapons Blueprints: " + ex.ToString());
                else setError(20, "Unable To Parse Weapons Inventory: " + ex.ToString());
            }
            finally {
                reader.Close(false);
                if (isBlueprint) this.weaponsBlueprints = tmpWeapons;
                else this.weaponsInventory = tmpWeapons;
            }
        }

        private void weaponsInventoryToData()
        {
            weaponsToData(TH1_SECTOR_WEAPONS);
        }

        private void weaponsBlueprintsToData()
        {
            weaponsToData(TH1_SECTOR_WEAPON_BLUEPRINTS);
        }

        private void weaponsToData( int sectorID )
        {
            byte[] tmpWeapons;
            int[] weapKeys = db.helper.weaponTypesDic.Keys.ToArray();
            List<TH1WeaponExt> list;

            switch(sectorID)
            {
                case TH1_SECTOR_WEAPONS:
                    list = weaponsInventory;
                    break;
                case TH1_SECTOR_WEAPON_BLUEPRINTS:
                    list = weaponsBlueprints;
                    break;
                default:
                    return;
            }

            // Sizing
            long bytesize = 4 * weapKeys.Count(); // Weapon Count Per Type
            bytesize += 4 * (weapKeys.Count() - 1); // Header Per Split Type (excl First)
            foreach (TH1WeaponExt tmpWeapon in list) bytesize += tmpWeapon.weaponExtToArray.Length; // Each Weapon
            tmpWeapons = new byte[bytesize];

            RWStream writer = new RWStream(tmpWeapons, true, true);
            try
            {
                foreach (int weap in db.helper.weaponWriteOrder)
                {
                    List<TH1WeaponExt> thisType = new List<TH1WeaponExt>();
                    foreach(TH1WeaponExt weaponLoop in list)
                    {
                        if (weaponLoop.weaponType == weap) thisType.Add(weaponLoop);
                    }

                    if(weap != db.helper.weaponWriteOrder[0]) writer.WriteBytes(new byte[] { 0x12, 0x34, 0x56, 0x78 });
                    writer.WriteUInt32((uint)thisType.Count);
                    foreach (TH1WeaponExt weaponWrite in thisType) writer.WriteBytes(weaponWrite.weaponExtToArray);
                }
            }
            catch (Exception ex) { setError(21, "Unable To Write Weapons: " + ex.ToString()); }
            finally { writer.Flush(); tmpWeapons = writer.ReadAllBytes(); writer.Close(true); }

            rebuildSector( sectorID, tmpWeapons );
        }

        #endregion Weapons IO

        #region Armour IO

        private void dataToArmourInventory()
        {
            dataToArmour(getSectorData(TH1_SECTOR_ARMOUR), false);
        }

        private void dataToArmourBlueprints()
        {
            dataToArmour(getSectorData(TH1_SECTOR_ARMOUR_BLUEPRINTS), true);
        }

        private void dataToArmour(byte[] sector, bool isBlueprint)
        {
            // Setup
            List<TH1ArmourExt> tmpArmour = new List<TH1ArmourExt>();
            TH1Helper helper = db.helper;
            string[] armourTypes = helper.armourTypesDic.Values.ToArray();

            // Buffer
            RWStream reader = new RWStream(sector, true);
            try
            {
                // Parse By Type
                for (int i = 0; i < armourTypes.Length; i++)
                {
                    uint armoCount = reader.ReadUInt32();
                    for (int armoNum = 0; armoNum < armoCount; armoNum++)
                    {
                        // Weapon Name
                        uint stringLength = reader.ReadUInt32();
                        byte[] nullString = reader.ReadBytes((int)stringLength);
                        TH1ArmourExt thisArmour = new TH1ArmourExt(db.armourCollection.findArmour(nullString));

                        // Inserted Runes
                        List<TH1RuneMExt> runesInserted = new List<TH1RuneMExt>();
                        uint runesInsertedCount = reader.ReadUInt32();
                        for (int runeNum = 0; runeNum < runesInsertedCount; runeNum++)
                        {
                            uint runeStringLength = reader.ReadUInt32();
                            byte[] runeNullString = reader.ReadBytes((int)runeStringLength);
                            TH1RuneMExt tmpRune = new TH1RuneMExt(db.runeMCollection.findRune(runeNullString));

                            tmpRune.purchased = reader.ReadUInt32();
                            tmpRune.dataB = reader.ReadUInt32();
                            tmpRune.valueModifier = reader.ReadUInt32();
                            tmpRune.dataD = reader.ReadUInt32();
                            tmpRune.paint = db.paintCollection.findPaint((int)reader.ReadUInt32());

                            runesInserted.Add(tmpRune);
                        }

                        // Weapon Ext
                        byte[] header = reader.ReadBytes(4); // header (meh)
                        thisArmour.craftedUint = reader.ReadUInt32();
                        thisArmour.valueB = reader.ReadUInt32();
                        thisArmour.condition = reader.ReadUInt32();
                        thisArmour.isEquiptUint = reader.ReadUInt32();
                        thisArmour.paint = db.paintCollection.findPaint((int)reader.ReadUInt32());
                        thisArmour.runesInserted = runesInserted;

                        // Add To Collection
                        tmpArmour.Add(thisArmour);
                    }

                    if (reader.Position < reader.Length) reader.ReadBytes(4);
                }
            }
            catch (Exception ex)
            {
                if (isBlueprint) setError(22, "Unable To Parse Armour Blueprints: " + ex.ToString());
                else setError(22, "Unable To Parse Armour Inventory: " + ex.ToString());
            }
            finally
            {
                reader.Close(false);
                if (isBlueprint) this.armourBlueprints = tmpArmour;
                else this.armourInventory = tmpArmour;
            }
        }

        private void armourInventoryToData()
        {
            armourToData(TH1_SECTOR_ARMOUR);
        }

        private void armourBlueprintsToData()
        {
            armourToData(TH1_SECTOR_ARMOUR_BLUEPRINTS);
        }

        private void armourToData(int sectorID)
        {
            byte[] tmpArmourBytes;
            int[] armourKeys = db.helper.armourTypesDic.Keys.ToArray();
            List<TH1ArmourExt> list;

            switch (sectorID)
            {
                case TH1_SECTOR_ARMOUR:
                    list = armourInventory;
                    break;
                case TH1_SECTOR_ARMOUR_BLUEPRINTS:
                    list = armourBlueprints;
                    break;
                default:
                    return;
            }

            // Sizing
            long bytesize = 4 * armourKeys.Count(); // Armour Count Per Type
            bytesize += 4 * (armourKeys.Count() - 1); // Header Per Split Type (excl First)
            foreach (TH1ArmourExt tmpArmour in list) bytesize += tmpArmour.armourExtToArray.Length; // Each Weapon
            tmpArmourBytes = new byte[bytesize];

            RWStream writer = new RWStream(tmpArmourBytes, true, true);
            try
            {
                foreach (int armo in db.helper.armourWriteOrder)
                {
                    List<TH1ArmourExt> thisType = new List<TH1ArmourExt>();
                    foreach (TH1ArmourExt armourLoop in list)
                    {
                        if (armourLoop.armourType == armo) thisType.Add(armourLoop);
                    }

                    if (armo != db.helper.armourWriteOrder[0]) writer.WriteBytes(new byte[] { 0x12, 0x34, 0x56, 0x78 });
                    writer.WriteUInt32((uint)thisType.Count);
                    foreach (TH1ArmourExt armourWrite in thisType) writer.WriteBytes(armourWrite.armourExtToArray);
                }
            }
            catch (Exception ex) { setError(23, "Unable To Write Armour: " + ex.ToString()); }
            finally { writer.Flush(); tmpArmourBytes = writer.ReadAllBytes(); writer.Close(true); }

            rebuildSector(sectorID, tmpArmourBytes);
        }

        #endregion Armour IO

        #region General Functions

        private void verifyRawData()
        {
            int peekSize = 4;
            var shortHead = new byte[peekSize];
            var shortPeek = new byte[peekSize];
            byte[] newhash;

            setError(0, "OK");

            if(this.rawData == null)
            {
                setError(1, "No Save Data Was Loaded");
                return;
            }

            RWStream readerW = new RWStream(this.rawData, true, true);
            try
            {
                if (!(readerW.Length >= TH1_LIMIT_MINSIZE)) // - Check #1 (Minimum Length)
                {
                    setError(2, "File Size Is Below Minimum");
                    return;
                }

                // grab the true data size (no padding / overflow)
                readerW.Position = TH1_OFFSET_SIZE;
                long dataSize = readerW.ReadInt32();
                if (!(readerW.Length >= dataSize))
                {
                    setError(3, "Data Size value is larger than the actual file-size");
                    return;
                }

                // trim the fat
                readerW.WriterBaseStream.SetLength(dataSize);

                // flush out the hash
                readerW.Position = TH1_OFFSET_HASH;
                this.hash = readerW.ReadBytes(PLACEHOLDER_HASH.Length);
                readerW.Position = TH1_OFFSET_HASH;
                readerW.WriteBytes(PLACEHOLDER_HASH);
                readerW.Flush();

            }
            catch (Exception ex) {
                setError(4,"IO Error Caught: " + ex.ToString());
                return;
            }
            finally {
                this.rawData = readerW.ReadAllBytes();
                readerW.Close(true);
            }

            newhash = getHash(this.rawData);
            this.hashVerified = this.hash.SequenceEqual(newhash);

        }

        #region Sector Loading

        private void loadGamesaveSectors()
        {
            byte[] deliminator = new byte[4];
            UInt32 thisSize = 0;
            long[] sectorsArray;
            List<long> _sectors;

            // Load
            Array.Copy(PUBLIC_HEADER, deliminator, deliminator.Length);

            RWStream reader = new RWStream(this.rawData, true);
            try
            {
                sectorsArray = reader.SearchHexString(BitConverter.ToString(deliminator).Replace("-", ""), false);
                _sectors = new List<long>(sectorsArray);

                if (_sectors.Count > 6)
                {
                    for (int cursec = 0; cursec < _sectors.Count; cursec++)
                    {
                        uint sectorSkip = 0;
                        if (cursec < (_sectors.Count - 1))
                        {
                            // Sector sepcific loading (as currently, the same deliminator is used within sector data)
                            switch (cursec)
                            {
                                case TH1_SECTOR_CHARMS_AVAILABLE:
                                case TH1_SECTOR_CHARMS_INCOMPLETE: // Charms
                                    reader.Position = _sectors[cursec] + deliminator.Length;
                                    sectorSkip = reader.ReadUInt32();
                                    thisSize = (UInt32)(_sectors[cursec + 1 + (int)sectorSkip] - _sectors[cursec]);
                                    break;
                                case TH1_SECTOR_WEAPONS:
                                case TH1_SECTOR_WEAPON_BLUEPRINTS:
                                    int weapCount = new TH1Helper().weaponTypesDic.Count;
                                    for (int i = 0; i < weapCount; i++)
                                    {
                                        reader.Position = _sectors[cursec + i + (int)sectorSkip] + deliminator.Length;
                                        sectorSkip += reader.ReadUInt32();
                                    }
                                    sectorSkip += (uint)weapCount - 1;
                                    thisSize = (UInt32)(_sectors[cursec + 1 + (int)sectorSkip] - _sectors[cursec]);
                                    break;
                                case TH1_SECTOR_ARMOUR:
                                case TH1_SECTOR_ARMOUR_BLUEPRINTS:
                                    int armourCount = new TH1Helper().armourTypesDic.Count;
                                    for (int i = 0; i < armourCount; i++)
                                    {
                                        reader.Position = _sectors[cursec + i + (int)sectorSkip] + deliminator.Length;
                                        sectorSkip += reader.ReadUInt32();
                                    }
                                    sectorSkip += (uint)armourCount - 1;
                                    thisSize = (UInt32)(_sectors[cursec + 1 + (int)sectorSkip] - _sectors[cursec]);
                                    break;
                                default: // Everything Else
                                    thisSize = (UInt32)(_sectors[cursec + 1] - _sectors[cursec]);
                                    break;
                            }
                        }
                        else thisSize = (UInt32)(this.rawData.Length - _sectors[cursec]);

                        thisSize -= (UInt32)deliminator.Length;

                        // Add the important references to the list
                        TH1Sector tmpsector = new TH1Sector(cursec);

                        // Sector Data
                        tmpsector.data = new byte[thisSize];
                        Array.Copy(this.rawData, _sectors[cursec] + deliminator.Length, tmpsector.data, 0, thisSize);

                        this.sectors.Add(tmpsector);

                        for (int i = 0; i < sectorSkip; i++) _sectors.RemoveAt(cursec + 1);
                    }
                }
                else
                {
                    setError(5, "Failed to load Gamesave sectors");
                }
            }
            catch { }
            finally { reader.Close(false); }
        }

        #endregion Sector Loading

        private byte[] getHash( byte[] saveData )
        {
            // Prepare Buffers
            var tmpres = new byte[PLACEHOLDER_HASH.Length];
            var checkBuff = new byte[(PUBLIC_HEADER.Length*2) + saveData.Length];

            // Load
            Array.Copy(abra(PUBLIC_HEADER),0,checkBuff,0, (PUBLIC_HEADER.Length * 2));
            Array.Copy(saveData, 0, checkBuff, (PUBLIC_HEADER.Length * 2), saveData.Length);

            // Calculate
            SHA1 sha = new SHA1CryptoServiceProvider();
            try
            {
                tmpres = sha.ComputeHash(checkBuff);
            } finally{ sha.Clear(); };

            // Free
            Array.Clear(checkBuff, 0, checkBuff.Length);

            return tmpres;
        }

        public byte[] rewriteHash( byte[] data)
        {
            // do stuff here
            byte[] newhash = getHash(data);
            RWStream writer = new RWStream(data, true, true);
            try
            {
                writer.Position = TH1_OFFSET_HASH;
                writer.WriteBytes(newhash);
            } catch(Exception ex) { setError(8,"Unable To Write New Hash: " + ex.ToString()); }
            finally { data = writer.ReadAllBytes(); writer.Close(true); }
            return data;
        }

        private void setError( long errno, string errmsg)
        {
            this.lastError = errno;
            this.lastErrorMsg = errmsg;
        }

        public void clearError()
        {
            if (this.rawData != null) setError(0, "OK.");
            else setError(-1, "Save Not Loaded.");
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        private void MsgBox( string message)
        {
            MessageBox.Show(message);
        }

        private string lookupDataPairName( uint dkey, int dpair)
        {
            switch (dpair)
            {
                case 1:
                    if (this.character.dataPairNamesA.ContainsKey(dkey)) return this.character.dataPairNamesA[dkey];
                    break;
                case 2:
                    if (this.character.dataPairNamesB.ContainsKey(dkey)) return this.character.dataPairNamesB[dkey];
                    break;
            }
            
            return dkey.ToString("X2");
        }

        private uint lookupDataPairValue(string dkey, int dpair)
        {
            switch (dpair)
            {
                case 1:
                    var keysA = this.character.dataPairNamesA.Where(kvp => kvp.Value == dkey).Select(kvp => kvp.Key).Take(1).ToList();
                    if (keysA.Count > 0) return keysA[0];
                    break;
                case 2:
                    var keysB = this.character.dataPairNamesB.Where(kvp => kvp.Value == dkey).Select(kvp => kvp.Key).Take(1).ToList();
                    if (keysB.Count > 0) return keysB[0];
                    break;
            }
            return uint.Parse(dkey, System.Globalization.NumberStyles.HexNumber);
        }

        #endregion General Functions

    }
}
