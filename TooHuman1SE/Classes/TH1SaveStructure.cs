﻿using System;
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
    */

    #region Sector
    public class TH1Sector
    {
        private long _id = 0;
        private long _offset = 0;
        private long _size = 0;
        private string _sectorname;

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
        public long offset
        {
            get
            {
                return _offset;
            }
            set
            {
                _offset = value;
            }
        }
        public long size
        {
            get
            {
                return _size;
            }
            set
            {
                _size = value;
            }
        }
        public string sizeString
        {
            get
            {
                string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
                int decimalPlaces = 1;
                long value = _size;

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
        public string sectorName
        {
            get
            {
                return _sectorname;
            }
        }
    }
    #endregion Sector

    public class TH1Helper
    {
        // Dictionaries
        private Dictionary<char,string> _colourNames = new Dictionary<char,string>() {
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
        private Dictionary<int, string> _weaponTypes = new Dictionary<int, string>()
        {
            {0 , ""},{1 , ""},{2 , ""},{3 , ""},{4 , ""},{5 , ""},{6 , ""},{7 , ""},{8 , ""},{9 , ""},{10 , ""},{11 , ""},{12 , ""},{13 , ""},{14 , ""},{15 , ""},{16 , ""},{17 , ""}
        };
        private Dictionary<int, string> _armourTypes = new Dictionary<int, string>()
        {
            {0 , ""},{1 , ""},{2 , ""},{3 , ""},{4 , ""},{5 , ""}
        };
        private Dictionary<int, string> _sectorNames = new Dictionary<int, string>()
        {
            {0, "header" },
            {1, "character" },
            {2, "skill_tree" },
            {3, "location" },
            {4, "charm-equipped" },
            {5, "runes" },
            {6, "charms" },
            {7, "weapons" },
            {8, "armour" },
            {11, "quests" },
            {12, "weapon-blueprints" },
            {13, "armour-blueprints" }
        };

        // Limits
        public int LIMIT_MAX_RUNES = 60;
        
        // Lookups
        public string getRuneColourName( char _colourID )
        {
            if( !_colourNames.TryGetValue(_colourID, out string tmpRes) ) tmpRes = "Unknown";
            return tmpRes;
        }
        public char getRuneColourID( string name)
        {
            char tmpRes = 'G';
            foreach( KeyValuePair<char,string> _kvp in _colourNames) if (_kvp.Value == name) tmpRes = _kvp.Key;
            return tmpRes;
        }
        public string[] colourNameArray
        {
            get
            {
                string[] tmpRes = new string[_colourNames.Count];
                int i = 0;
                foreach( KeyValuePair<char, string> _kvp in _colourNames)
                {
                    tmpRes[i] = _kvp.Value;
                    i++;
                }
                return tmpRes;
            }
        }
        public string getRuneName(int _runeID)
        {
            if (!_runeNames.TryGetValue(_runeID, out string tmpRes)) tmpRes = "Unknown";
            return tmpRes;
        }
        public string[] classNamesArray
        {
            get
            {
                return new string[] { "Berserker", "Champion", "Defender", "Heavy Gunner", "Gunslinger", "Commando", "Bio-Engineer", "Dragon", "Rune Master" };
            }
        }
        public string[] alignmentNamesArray
        {
            get
            {
                return new string[] { "None", "Human", "Cybernetics" }; 
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
    }

    public class TH1Character
    {
        // Offsets
        public long OFFSET_HEADER = 0;
        public long OFFSET_SAVESLOT = 4;
        public long OFFSET_NAME_U = 20;
        public long OFFSET_CLASS = 52;
        public long OFFSET_LEVELA = 56;
        public long OFFSET_ENEMIES_KILLED = 124;
        public long OFFSET_SKILLPOINTS = 128;
        public long OFFSET_DATA_PAIRSA = 956;
        public long OFFSET_DATA_PAIRSB = 1652;
        public long OFFSET_LEVELB = 1748;
        public long OFFSET_CURR_LEVEL_EXP = 1752;
        public long OFFSET_EXP = 1756;
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

        public long OFFSET_ALIGNMENT = 0; // After Ascii Name (uint 14/14)

        // Data Pairs
        public Dictionary<string, uint> dataPairsA = new Dictionary<string, uint>();
        public Dictionary<string, uint> dataPairsB = new Dictionary<string, uint>();

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
            [0x7B] = "item_pickups"
        };
        public Dictionary<uint, string> dataPairNamesB = new Dictionary<uint, string>
        {

        };

        
    }

    #region Skills Tree
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
    #endregion Skills Tree

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
                if(runeBonus.bonusName == "Colour Module") return string.Format("{0}", runeBonus.bonusName);
                else return string.Format("{0} +{1}%", runeBonus.bonusName, runeBonusValue);
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
                else return string.Format("{1} {2}", runeID, runeLevel, bonusName);
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

        // The Support
        private TH1RuneMBonusCollection _bonusCollection = new TH1RuneMBonusCollection();

        // Create
        public TH1RuneMCollection()
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
                TH1RuneMBonus _bonus = _bonusCollection.findBonus(_rune.BonusID);
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

    #region TH1Weapon

    public class TH1Weapon
    {

    }

    #endregion Weapon

    #region TH1Armour

    public class TH1Armour
    {

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
        public const int TH1_SECTOR_CHARM_EQUIP = 4; // Equipt Charms? - 2x Only, NOID = No Rune
        public const int TH1_SECTOR_RUNE = 5; // Rune Inventry
        public const int TH1_SECTOR_CHARMS = 6; // Charms Inventry -- uint #of quests, string size, name, uint 0x00, 0x123456, data length 0x4C, back to string size 
        public const int TH1_SECTOR_WEAPONS = 7; // Weapons -- uint #of weapons, string size, name, uint 0x00, 0x123456, data length 0x14, back to string size
        public const int TH1_SECTOR_ARMOUR = 8; // Armour -- uint #of Armour, 
        public const int TH1_SECTOR_UNKNOWN01 = 9;
        public const int TH1_SECTOR_UNKNOWN02 = 10;
        public const int TH1_SECTOR_QUESTS = 11; // ?
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
        private static readonly byte[] PUBLIC_FOOTER = new byte[]{
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
        private static readonly byte[] PUBLIC_HEADER = new byte[]{
            0x12, 0x34, 0x56, 0x78, 0x00, 0x00, 0x00, 0x30,
            0x54, 0x48, 0x31, 0x00, 0x00, 0x00, 0x00, 0x42
        };

        //  Is this the chicken or the egg?
        private static readonly byte[] PLACEHOLDER_HASH = new byte[]
        {
            0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        #endregion Constants

        #region Variables

        // General Variables
        public string filePath;
        public int saveType;
        public byte[] rawData;
        public byte[] hash;
        public Boolean hashVerified;
        public long dataSize;

        // Gamesave Parsing
        public List<TH1Sector> sectors;
        public TH1Character character;
        public List<TH1RuneMExt> runes;
        public TH1SkillsTree skills;

        // Problems
        // public Boolean saveLoaded = false;
        public long lastError;       // current 0
        public string lastErrorMsg;

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
            this.dataSize = 0;

            // Parsed
            this.sectors = new List<TH1Sector>();
            this.character = new TH1Character();
            this.runes = new List<TH1RuneMExt>();
            this.skills = new TH1SkillsTree(0);

            // Checks
            setError(-1, "Save Not Loaded.");
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
                loadGamesaveSectors();
                dataToCharacter();
                dataToSkillsTree();
                dataToRunes();
                // dataToCharms();

                this.dataSize = this.rawData.Length;
            }

        }

        public void writeSaveFile(string outFilePath)
        {
            byte[] newhash;
            byte[] saveOut;

            // Save Settings
            characterToData();
            skillsTreeToData();
            runesToData();

            // Create the Save Buffer in Memory
            saveOut = new byte[this.dataSize];
            Array.Copy(this.rawData, 0, saveOut, 0, saveOut.Length);
            Array.Resize(ref saveOut, (int)(this.dataSize + PUBLIC_FOOTER.Length));
            Array.Copy(PUBLIC_FOOTER, 0, saveOut, this.dataSize, PUBLIC_FOOTER.Length);

            // Generate Hash
            newhash = getHash(saveOut);

            // Overwrite the Placeholder Hash
            RWStream writer = new RWStream(saveOut, true, true);
            try
            {
                writer.Position = TH1_OFFSET_HASH;
                writer.WriteBytes(newhash);
                writer.Flush();
            }
            catch (Exception ex) {
                setError(8, "Unable To Write New Hash: " + ex.ToString());
            }
            finally { saveOut = writer.ReadAllBytes(); writer.Close(true); }

            try
            {
                using (var fs = new FileStream(outFilePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(saveOut, 0, saveOut.Length);
                }
            }
            catch (Exception ex)
            {
                setError(7, "Unable To Write Gamesave: " + ex.ToString());
            }

        }

        // Drop Them all
        public void writeAllSectors(bool incHeader)
        {
            string tmphash = BitConverter.ToString(this.hash).Replace("-", "");
            Directory.CreateDirectory("tmp");
            Directory.CreateDirectory(@"tmp\" + tmphash);
            for (int i = 0; i < this.sectors.Count; i++)
            {
                writeSectorToFile(@"tmp\" + tmphash + @"\sector" + i.ToString("000") + ".tmp", i, incHeader);
            }
        }

        #endregion Public

        #region IO Functions

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

        // Dumping Sectors..
        public void writeSectorToFile(string filename, int sectorID, bool incHeader)
        {
            // Sector Code
            byte[] sectorcode = new byte[4];
            Array.Copy(PUBLIC_HEADER, sectorcode, sectorcode.Length);

            // Write All Sectors
            if ((this.lastError == 0) && (sectorID <= this.sectors.Count) && (sectorID >= 0))
            {
                // Grab Data
                byte[] sectordata = new byte[this.sectors[sectorID].size];
                Array.Copy(this.rawData, this.sectors[sectorID].offset, sectordata, 0, sectordata.Length);

                // Write It
                RWStream writer = new RWStream(File.Open(filename, FileMode.Create), true);
                if (incHeader)
                {
                    writer.WriteBytes(sectorcode);
                }
                writer.WriteBytes(sectordata);
                writer.Flush();
                writer.Close(false);
            }
        }

        // For Those Constantly Changing Sectors ..
        private void replaceSector(int sectorID, byte[] newData)
        {

            byte[] reconstructed;
            long pos = 0;
            long relSize = 0;
            reconstructed = new byte[this.rawData.Length - this.sectors[sectorID].size + newData.Length];

            // Beginning
            Array.Copy(this.rawData, 0, reconstructed, pos, this.sectors[sectorID].offset);
            pos += this.sectors[sectorID].offset;

            // Middle
            Array.Copy(newData, 0, reconstructed, pos, newData.Length);
            pos += newData.Length;

            // End
            long secondHalf = this.sectors[sectorID].offset + this.sectors[sectorID].size;
            Array.Copy(this.rawData, secondHalf, reconstructed, pos, this.rawData.Length - secondHalf);

            // Shuffle Sector Offsets
            relSize = newData.Length - this.sectors[sectorID].size;
            this.sectors[sectorID].size = newData.Length;
            for (int sectorI = sectorID + 1; sectorI < this.sectors.Count; sectorI++)
            {
                this.sectors[sectorI].offset += relSize;
            }

            // Output
            this.rawData = reconstructed;
            this.dataSize = this.rawData.Length;
            rewriteFileSize();
        }

        private void rewriteFileSize()
        {
            long tmpOffset = TH1_OFFSET_SIZE;
            RWStream writer = new RWStream(this.rawData, true, true);
            try
            {
                writer.Position = tmpOffset;
                writer.WriteUInt32((uint)this.dataSize);
            }
            catch (Exception ex) { setError(11, "Unable To Write New Filesize: " + ex.ToString()); }
            finally { this.rawData = writer.ReadAllBytes(); writer.Close(true); }
        }

        #endregion IO Functions

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
            // - Data Pairs A
            // - Data Pairs B

            // temp load the character data
            byte[] charData = new byte[this.sectors[TH1_SECTOR_CHARACTER].size];
            Array.Copy(this.rawData, this.sectors[TH1_SECTOR_CHARACTER].offset, charData, 0, charData.Length);
            TH1Character tmpChar = this.character;

            RWStream reader = new RWStream(charData, true);
            try
            {

                // Character Name
                reader.Position = tmpChar.OFFSET_NAME_A_LENGTH;
                int namelength = (int)reader.PeekUInt32();
                reader.Position = tmpChar.OFFSET_NAME_A;
                tmpChar.name = reader.ReadString(StringType.Ascii, namelength - 1);

                tmpChar.OFFSET_ALIGNMENT = tmpChar.OFFSET_NAME_A_LENGTH + 4 + namelength + (13*4);
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

                reader.Position = tmpChar.OFFSET_SKILLPOINTS;
                tmpChar.skillPoints = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_SAVESLOT;
                tmpChar.saveSlot = reader.ReadUInt32();

                reader.Position = tmpChar.OFFSET_DATA_PAIRSA;
                for (int i = 0; i < tmpChar.LIMIT_DATA_PAIRSA; i++)
                {
                    tmpChar.dataPairsA.Add(lookupDataPairName(reader.ReadUInt32(), 1), reader.ReadUInt32());
                }

                reader.Position = tmpChar.OFFSET_DATA_PAIRSB;
                for (int i = 0; i < tmpChar.LIMIT_DATA_PARISB; i++)
                {
                    tmpChar.dataPairsB.Add(lookupDataPairName(reader.ReadUInt32(), 2), reader.ReadUInt32());
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
            // - Data Pairs A
            // - Data Pairs B

            // temp load the character data
            byte[] charData = new byte[this.sectors[TH1_SECTOR_CHARACTER].size];
            byte[] charDataTemp = new byte[1];

            Array.Copy(this.rawData, this.sectors[TH1_SECTOR_CHARACTER].offset, charData, 0, charData.Length);
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

                tmpChar.OFFSET_ALIGNMENT = tmpChar.OFFSET_NAME_A + 4 + newNameLength + 1 + (13 * 4);

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
                writer.Position = tmpChar.OFFSET_SKILLPOINTS;
                writer.WriteUInt32((uint)tmpChar.skillPoints);

                // Write Save Slot
                writer.Position = tmpChar.OFFSET_SAVESLOT;
                writer.WriteUInt32((uint)tmpChar.saveSlot);

                writer.Position = tmpChar.OFFSET_DATA_PAIRSA;
                foreach (KeyValuePair<string, uint> dp in tmpChar.dataPairsA)
                {
                    writer.WriteUInt32(lookupDataPairValue(dp.Key, 1));
                    writer.WriteUInt32(dp.Value);
                }

                writer.Position = tmpChar.OFFSET_DATA_PAIRSB;
                foreach (KeyValuePair<string, uint> dp in tmpChar.dataPairsB)
                {
                    writer.WriteUInt32(lookupDataPairValue(dp.Key, 2));
                    writer.WriteUInt32(dp.Value);
                }

                //tmpChar.playtime;

            }
            catch (Exception ex) { setError(10, "Unable To Write Character Stats To Save Data: " + ex.ToString()); return; }
            finally { writer.Flush(); charData = writer.ReadAllBytes(); writer.Close(false); }

            replaceSector(TH1_SECTOR_CHARACTER, charData);

        }

        #endregion Character IO

        #region Skills Tree IO

        private void dataToSkillsTree()
        {
            // Reset With Default Class
            TH1SkillsTree tmpSkills = new TH1SkillsTree(0);

            // Buffer It
            byte[] skillsData = new byte[this.sectors[TH1_SECTOR_SKILLTREE].size];
            Array.Copy(this.rawData, this.sectors[TH1_SECTOR_SKILLTREE].offset, skillsData, 0, skillsData.Length);

            RWStream reader = new RWStream(skillsData, true);
            try
            {
                // Read In Character Class
                tmpSkills = new TH1SkillsTree(reader.ReadUInt32()); // New With Alignment

                while ( reader.Position < reader.Length)
                {
                    TH1SkillsTreePair stp = new TH1SkillsTreePair();
                    stp.first = reader.ReadUInt32();
                    stp.second = reader.ReadUInt32();
                    tmpSkills.pairs.Add(stp);
                }

            }
            catch (Exception ex) { setError( 12, "Unable To Parse Skills Tree: " + ex.ToString()); }
            finally { reader.Close(false); this.skills = tmpSkills; }
        }

        private void skillsTreeToData()
        {
            // Buffer It
            byte[] skillsData = new byte[(this.skills.pairs.Count*8)+4];

            RWStream writer = new RWStream(skillsData, true, true);
            try
            {
                writer.WriteUInt32((uint)this.character.charClass);

                for( int num=0; num < this.skills.pairs.Count; num++)
                {
                    writer.WriteUInt32((uint)this.skills.pairs[num].first);
                    writer.WriteUInt32((uint)this.skills.pairs[num].second);
                }
            }
            catch (Exception ex) { setError(13, "Unable To Write Skills Tree: " + ex.ToString()); }
            finally { writer.Flush(); skillsData = writer.ReadAllBytes(); writer.Close(false); }

            replaceSector(TH1_SECTOR_SKILLTREE, skillsData);
        }

        #endregion Skills Tree IO

        #region Runes IO

        private void dataToRunes()
        {
            List<TH1RuneMExt> tmpRunes = new List<TH1RuneMExt>();
            TH1RuneMCollection runeCollection = new TH1RuneMCollection();
            TH1PaintCollection paintCollection = new TH1PaintCollection();

            // Buffer It
            byte[] runesData = new byte[this.sectors[TH1_SECTOR_RUNE].size];
            Array.Copy(this.rawData, this.sectors[TH1_SECTOR_RUNE].offset, runesData, 0, runesData.Length);

            RWStream reader = new RWStream(runesData, true);
            try
            {
                int runeCount = (int)reader.ReadUInt32();

                for (int runeloop = 0; runeloop < runeCount; runeloop++)
                {
                    int nameLength = (int)reader.ReadUInt32();
                    byte[] runeName = reader.ReadBytes(nameLength);

                    TH1RuneMExt tmpRune = new TH1RuneMExt( runeCollection.findRune(runeName) );
                    if (tmpRune.rune == null) tmpRune = new TH1RuneMExt(new TH1RuneM());

                    tmpRune.purchased = reader.ReadUInt32();
                    tmpRune.dataB = reader.ReadUInt32();
                    tmpRune.valueModifier = reader.ReadUInt32();
                    tmpRune.dataD = reader.ReadUInt32();
                    tmpRune.paint = paintCollection.findPaint((int)reader.ReadUInt32());

                    tmpRunes.Add(tmpRune);
                }

            }
            catch (Exception ex) { setError(14, "Unable To Parse Runes: " + ex.ToString()); }
            finally { reader.Close(false); this.runes = tmpRunes; }

        }

        private void runesToData()
        {
            long bytesize = 0;
            foreach( TH1RuneMExt tmpRune in this.runes) bytesize += tmpRune.runeExtToArray.Length; 
            byte[] tmpRunes = new byte[4+bytesize];

            RWStream writer = new RWStream(tmpRunes, true, true);
            try
            {
                writer.WriteUInt32((uint)this.runes.Count);
                foreach (TH1RuneMExt runeLoop in this.runes) writer.WriteBytes(runeLoop.runeExtToArray);
            } catch( Exception ex) { setError(15, "Unable To Write Runes: " + ex.ToString()); }
            finally { writer.Flush(); tmpRunes = writer.ReadAllBytes(); writer.Close(true); }

            replaceSector(TH1_SECTOR_RUNE, tmpRunes);
        }

        #endregion Runes IO

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
                this.dataSize = readerW.ReadInt32();
                if (!(readerW.Length >= this.dataSize))
                {
                    setError(3, "Data Size value is larger than the actual file-size");
                    return;
                }

                // trim the fat
                readerW.WriterBaseStream.SetLength(this.dataSize);

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

            newhash = getHash();
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
                sectorsArray = reader.SearchHexString(BitConverter.ToString(deliminator).Replace("-",""), false);
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
                                case TH1_SECTOR_CHARMS: // Charms
                                    reader.Position = _sectors[cursec] + deliminator.Length;
                                    sectorSkip = reader.ReadUInt32();
                                    thisSize = (UInt32)(_sectors[cursec + 1 + (int)sectorSkip] - _sectors[cursec]);
                                    break;
                                case TH1_SECTOR_WEAPONS:
                                case TH1_SECTOR_WEAPON_BLUEPRINTS:
                                    int weapCount = new TH1Helper().weaponTypesDic.Count;
                                    for ( int i=0; i < weapCount; i++)
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
                                case TH1_SECTOR_QUESTS:
                                    reader.Position = _sectors[cursec] + deliminator.Length;
                                    sectorSkip = reader.ReadUInt32();
                                    thisSize = (UInt32)(_sectors[cursec + 1 + (int)sectorSkip] - _sectors[cursec]);
                                    break;
                                default: // Everything Else
                                    thisSize = (UInt32)(_sectors[cursec + 1] - _sectors[cursec]);
                                    break;
                            }
                        } else thisSize = (UInt32)(this.dataSize - _sectors[cursec]);

                        thisSize -= (UInt32)deliminator.Length;

                        // Add the important references to the list
                        TH1Sector tmpsector = new TH1Sector();
                        tmpsector.id = cursec;
                        tmpsector.offset = _sectors[cursec] + deliminator.Length;
                        tmpsector.size = thisSize;
                        this.sectors.Add(tmpsector);
                        // cursec += (int)sectorSkip;
                        for (int i = 0; i < sectorSkip; i++) _sectors.RemoveAt(cursec + 1);
                    }
                }
                else {
                    setError(5, "Failed to load Gamesave sectors");
                }
            }
            catch { }
            finally { reader.Close(false);  }
        }

        #endregion Sector Loading

        private byte[] getHash( byte[] saveData )
        {
            // Prepare Buffers
            var tmpres = new byte[PLACEHOLDER_HASH.Length];
            var checkBuff = new byte[(PUBLIC_HEADER.Length*2) + this.dataSize];

            // Load
            Array.Copy(abra(PUBLIC_HEADER),0,checkBuff,0, (PUBLIC_HEADER.Length * 2));
            Array.Copy(this.rawData, 0, checkBuff, (PUBLIC_HEADER.Length * 2), this.dataSize);

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

        // Overload
        private byte[] getHash()
        {
            return getHash(this.rawData);
        }

        private void setError( long errno, string errmsg)
        {
            this.lastError = errno;
            this.lastErrorMsg = errmsg;
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
