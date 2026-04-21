using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;

namespace FF4FE_ERTracker
{
    // ─────────────────────────────────────────────────────────────────────────
    // Entrance coordinate table — from your learned lua table
    // Keys are "OW(x,y)", "UW(x,y)", "Moon(x,y)"
    // ─────────────────────────────────────────────────────────────────────────
    internal static class EntranceTable
    {
        public static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ── Overworld ────────────────────────────────────────────────────
            { "OW(101,157)",  "Baron"                },
            { "OW(101,156)",  "Baron"                },
            { "OW(102,157)",  "Baron"                },
            { "OW(102,156)",  "Baron"                },
            { "OW(103,158)",  "Town of Baron"        },
            { "OW(103,157)",  "Town of Baron"        },
            { "OW(100,157)",  "Town of Baron"        },
            { "OW(100,158)",  "Town of Baron"        },
            { "OW(104,215)",  "Agart"                },
            { "OW(119,58)",   "Damcyan"              },
            { "OW(125,104)",  "Kaipo"                },
            { "OW(125,66)",   "Lake"                 },
            { "OW(134,72)",   "Waterfalls"           },
            { "OW(136,56)",   "Antlion"              },
            { "OW(138,77)",   "Watery Pass North"    },
            { "OW(138,83)",   "Watery Pass South"    },
            { "OW(152,49)",   "Mt.Hobs West"         },
            { "OW(154,199)",  "Mysidia"              },
            { "OW(155,199)",  "Mysidia"              },
            { "OW(160,49)",   "Mt.Hobs East"         },
            { "OW(210,130)",  "Silvera"              },
            { "OW(213,209)",  "Ordeal's Forest"      },
            { "OW(215,58)",   "Fabul"                },
            { "OW(218,199)",  "Mt.Ordeals"           },
            { "OW(219,136)",  "Grotto Adamant"       },
            { "OW(228,47)",   "Fabul Forest"         },
            { "OW(24,231)",   "Cave Eblan"           },
            { "OW(34,101)",   "Toroian Forest"       },
            { "OW(35,81)",    "Toroian Castle"       },
            { "OW(35,82)",    "Toroian Castle"       },
            { "OW(35,83)",    "Town of Toroia"       },
            { "OW(36,84)",    "Town of Toroia"       },
            { "OW(41,53)",    "Chocobo's Village"    },
            { "OW(45,236)",   "Eblan"                },
            { "OW(74,53)",    "Cave Magnes"          },
            { "OW(76,132)",   "Misty Cave South"     },
            { "OW(84,119)",   "Misty Cave North"     },
            { "OW(86,79)",    "Center Forest"        },
            { "OW(89,163)",   "Baron Forest"         },
            { "OW(96,119)",   "Village Mist Left"    },
            { "OW(97,119)",   "Village Mist Right"   },

            // ── Underworld ───────────────────────────────────────────────────
            { "UW(100,82)",   "Castle of Dwarves"    },
            { "UW(104,123)",  "Kokkol the Smith's"   },
            { "UW(13,14)",    "Sylvan Cave"          },
            { "UW(27,86)",    "Land of Monsters"     },
            { "UW(46,109)",   "Sealed Cave"          },
            { "UW(48,16)",    "Tower of Bab-il"      },
            { "UW(62,121)",   "Tomra"                },
            { "UW(91,83)",    "Dwarf Base"           },

            // ── Moon ─────────────────────────────────────────────────────────
            { "Moon(18,14)",  "Lunar Path"           },
            { "Moon(18,20)",  "Lunar Path"           },
            { "Moon(28,25)",  "Lunar's Lair"         },
            { "Moon(40,28)",  "Lunar Path"           },
            { "Moon(41,24)",  "Lunar Path"           },
            { "Moon(61,23)",  "Cave Bahamut"         },
        };

        public static string Lookup(string plane, int x, int y)
        {
            string key = $"{plane}({x},{y})";
            return Map.TryGetValue(key, out string name) ? name : null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FF4 character decoding
    // ─────────────────────────────────────────────────────────────────────────
    internal static class FF4Encoding
    {
        private static readonly Dictionary<byte, char> CharMap = BuildMap();

        private static Dictionary<byte, char> BuildMap()
        {
            var m = new Dictionary<byte, char>();
            for (int i = 0x42; i <= 0x5B; i++) m[(byte)i] = (char)(i - 1);
            for (int i = 0x5C; i <= 0x75; i++) m[(byte)i] = (char)(i + 5);
            for (int i = 0x80; i <= 0x89; i++) m[(byte)i] = (char)(i - 0x80 + 0x30);
            m[0xC0] = '\''; m[0xC1] = '.'; m[0xC2] = ' ';
            m[0xC8] = ',';  m[0xC9] = '!'; m[0x76] = ' ';
            return m;
        }

        public static string ReadNameBanner(Func<long, byte> read, long baseAddr)
        {
            var sb = new StringBuilder();
            bool hasPad = false, any = false;
            for (int i = 0; i < 32; i++)
            {
                byte b = read(baseAddr + i);
                if (b == 0xFF)
                {
                    hasPad = true;
                    if (any && (sb.Length == 0 || sb[sb.Length - 1] != ' '))
                        sb.Append(' ');
                }
                else if (b == 0x00) { }
                else if (CharMap.TryGetValue(b, out char ch)) { sb.Append(ch); any = true; }
                else return null;
            }
            if (!hasPad || !any) return null;
            string s = sb.ToString().Trim();
            return (s.Length == 0 || s.Length > 24) ? null : s;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pending transition (waiting for destination name banner)
    // ─────────────────────────────────────────────────────────────────────────
    internal class Pending
    {
        public string FromLabel;
        public string FromCoord;  // raw "OW(x,y)" for dedup of multi-entrance locations
        public int    DestMap;
        public int    DestPlane;
        public int    FramesWaited;
        public const int NameWindow = 300;  // frames after trans-end to accept a banner
        public const int Timeout    = 600;  // frames before giving up entirely
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multi-column connection display — wraps to new column instead of scrolling
    // ─────────────────────────────────────────────────────────────────────────
    internal sealed class ConnectionPanel : Panel
    {
        private readonly List<string> _items = new List<string>();
        private readonly Font  _font  = new Font("Consolas", 9.5f);
        private readonly Brush _fgBrush    = new SolidBrush(Color.FromArgb(220, 220, 235));
        private readonly Brush _arrowBrush = new SolidBrush(Color.FromArgb(100, 180, 255));
        private const int ROW_H   = 20;
        private const int COL_GAP = 20;

        public int ItemCount => _items.Count;

        public ConnectionPanel()
        {
            DoubleBuffered = true;
            BackColor      = Color.FromArgb(18, 18, 24);
        }

        public void AddItem(string text) { _items.Add(text); Invalidate(); }
        public new void Clear()          { _items.Clear();   Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_items.Count == 0) return;

            var g = e.Graphics;
            int rowsPerCol = Math.Max(1, (ClientSize.Height - 6) / ROW_H);

            // Find widest item to set uniform column width
            float maxW = 0;
            foreach (var item in _items)
            {
                float w = g.MeasureString(item, _font).Width;
                if (w > maxW) maxW = w;
            }
            int colW = (int)maxW + COL_GAP;

            int col = 0, row = 0;
            foreach (var item in _items)
            {
                int px = col * colW + 6;
                int py = row * ROW_H + 4;

                int arrowIdx = item.IndexOf('→');
                if (arrowIdx >= 0)
                {
                    string left  = item.Substring(0, arrowIdx);
                    string arrow = "→";
                    string right = item.Substring(arrowIdx + 1);
                    float  lw    = g.MeasureString(left,  _font).Width;
                    float  aw    = g.MeasureString(arrow, _font).Width;
                    g.DrawString(left,  _font, _fgBrush,    px,       py);
                    g.DrawString(arrow, _font, _arrowBrush, px + lw,  py);
                    g.DrawString(right, _font, _fgBrush,    px + lw + aw, py);
                }
                else
                {
                    g.DrawString(item, _font, _fgBrush, px, py);
                }

                if (++row >= rowsPerCol) { row = 0; col++; }
            }
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main ExternalTool window
    // ─────────────────────────────────────────────────────────────────────────
    [ExternalTool("FF4FE ER Tracker")]
    public sealed class ERTrackerForm : Form, IExternalToolForm
    {
        // ── RAM addresses ────────────────────────────────────────────────────
        private const long A_LOCAL_MAP = 0x7E1702;
        private const long A_PLAYER_X  = 0x7E1706;
        private const long A_PLAYER_Y  = 0x7E1707;
        private const long A_TRANS     = 0x7E06D9;
        private const long A_ON_OW     = 0x7E0CDD;
        private const long A_PLANE     = 0x7E1701;
        private const long A_NAME_SHOW = 0x7E0649;
        private const long A_NAME_TEXT = 0x7E0774;
        private const long A_IN_BATTLE = 0x7E0140;

        private static readonly string[] PlanePrefix = { "OW", "UW", "Moon" };

        // ── BizHawk API ──────────────────────────────────────────────────────
        public ApiContainer APIs { get; set; }
        private IMemoryApi Mem => APIs?.Memory;

        // ── IToolForm ────────────────────────────────────────────────────────
        public bool AskSaveChanges() => true;
        public bool IsActive { get; private set; }
        public bool IsLoaded { get; private set; }

        // ── Tracker state ─────────────────────────────────────────────────────
        private bool   _init;
        private int    _mapId;
        private bool   _onOW;
        private bool   _prevTrans, _prevName;
        private int    _surfX, _surfY, _surfPlane;

        // Latched at transition START — the entrance we stepped into
        private string _fromLabel;   // null means no active transition to record
        private string _fromCoord;   // raw "OW(x,y)" for multi-entrance dedup
        private bool   _fromOnOW;

        private Pending _pending;

        private readonly HashSet<int> _transitMaps = new HashSet<int> { 0x2F };
        private readonly Dictionary<int, HashSet<int>> _planesSeen  = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, string>        _nameCache   = new Dictionary<int, string>();
        private readonly HashSet<string>                _knownPairs  = new HashSet<string>(StringComparer.Ordinal);

        // Entrances with multiple OW tiles — each tile is a distinct entrance,
        // so dedup uses coord+name rather than name alone.
        private static readonly HashSet<string> MultiEntrance = new HashSet<string>(StringComparer.Ordinal)
        {
            "Lunar Path",       // 4 moon tiles
            "Misty Cave North", // separate N/S tiles kept distinct
            "Misty Cave South",
            "Baron",            // multiple castle/town adjacent tiles
            "Town of Baron",
            "Mysidia",
            "Toroian Castle",
            "Town of Toroia",
        };
        private ConnectionPanel _panel;
        private Label           _statusLabel;
        private Label           _countLabel;

        // ─────────────────────────────────────────────────────────────────────
        public ERTrackerForm()
        {
            BuildUI();
            IsLoaded = true;
            IsActive = true;
        }

        // ── IExternalToolForm ─────────────────────────────────────────────────
        public void Restart()
        {
            _init = false; _pending = null; _fromLabel = null;
            _prevTrans = false; _prevName = false;
            UpdateStatus("Restarted.");
        }

        public void UpdateValues(ToolFormUpdateType type)
        {
            if (type != ToolFormUpdateType.PostFrame) return;
            if (Mem == null) return;
            try { Tick(); }
            catch (Exception ex) { UpdateStatus("Error: " + ex.Message); }
        }

        // ── Per-frame logic ───────────────────────────────────────────────────
        private byte Rd(long addr) => (byte)Mem.ReadByte(addr, "System Bus");
        private string PlaneStr(int p) => p < PlanePrefix.Length ? PlanePrefix[p] : "P" + p;

        private void Tick()
        {
            int  mapId  = Rd(A_LOCAL_MAP);
            int  x      = Rd(A_PLAYER_X);
            int  y      = Rd(A_PLAYER_Y);
            bool trans  = Rd(A_TRANS)     == 1;
            bool onOW   = Rd(A_ON_OW)     != 0;
            int  plane  = Rd(A_PLANE);
            bool inBat  = Rd(A_IN_BATTLE) == 1;
            bool nameOn = Rd(A_NAME_SHOW) != 0;

            // onOW is only true for overworld (plane 0). UW and Moon surfaces
            // also have onOW=true in FF4 — but just in case, treat any non-interior
            // state as a surface: if the player is not in battle and plane matches
            // a surface plane, we treat it as "on surface".
            bool onAnySurface = onOW; // keep using the hardware flag as primary

            // First frame
            if (!_init)
            {
                _mapId = mapId; _onOW = onOW;
                _surfX = x; _surfY = y; _surfPlane = plane;
                _init = true;
                UpdateStatus("Tracking…");
            }

            // Track surface position (all planes)
            if (onAnySurface && !trans)
            {
                _surfX = x; _surfY = y; _surfPlane = plane;
            }

            // Name banner appeared
            if (nameOn && !_prevName)
            {
                string n = FF4Encoding.ReadNameBanner(Rd, A_NAME_TEXT);
                if (n != null)
                {
                    _nameCache[mapId] = n;
                    if (_pending != null && _pending.FramesWaited <= Pending.NameWindow)
                        ResolvePending(n);
                }
            }

            // Tick pending timeout
            if (_pending != null)
            {
                _pending.FramesWaited++;
                if (_pending.FramesWaited >= Pending.Timeout)
                    ResolvePending(null);
            }

            // ── Transition START ──────────────────────────────────────────────
            if (trans && !_prevTrans && !inBat)
            {
                if (_pending != null) ResolvePending(null);

                if (_onOW)
                {
                    // Walking into an entrance from any surface — this is what we track
                    string name = EntranceTable.Lookup(PlaneStr(_surfPlane), _surfX, _surfY);
                    _fromLabel = name ?? $"{PlaneStr(_surfPlane)}({_surfX},{_surfY})";
                    _fromCoord = $"{PlaneStr(_surfPlane)}({_surfX},{_surfY})";
                    _fromOnOW  = true;
                }
                else
                {
                    // Interior → anywhere: this is an exit — do not record
                    _fromLabel = null;
                    _fromCoord = null;
                    _fromOnOW  = false;
                }
            }

            // ── Transition END ────────────────────────────────────────────────
            if (!trans && _prevTrans && !inBat && _fromLabel != null)
            {
                // OW → OW (airship) — skip
                if (_fromOnOW && onOW)
                {
                    _fromLabel = null; _fromCoord = null; goto Done;
                }

                // Arriving at transit map — skip
                if (_transitMaps.Contains(mapId))
                {
                    _fromLabel = null; _fromCoord = null; goto Done;
                }

                // Create pending record, wait for name banner
                _pending = new Pending
                {
                    FromLabel    = _fromLabel,
                    FromCoord    = _fromCoord,
                    DestMap      = mapId,
                    DestPlane    = plane,
                    FramesWaited = 0,
                };

                // Try to resolve immediately in priority order:
                // 1. Name banner already on screen
                // 2. Previously cached name for this map
                // 3. Leave pending — banner will fire within NameWindow frames
                if (nameOn)
                {
                    string n = FF4Encoding.ReadNameBanner(Rd, A_NAME_TEXT);
                    if (n != null) { _nameCache[mapId] = n; ResolvePending(n); }
                }
                else if (_nameCache.TryGetValue(mapId, out string cached))
                {
                    ResolvePending(cached);
                }
                // If neither, ResolvePending will be called when the banner fires
                // or after Timeout frames (which uses Map_XX fallback)

                _fromLabel = null; _fromCoord = null;
            }

            Done:
            _prevTrans = trans;
            _prevName  = nameOn;
            _mapId     = mapId;
            _onOW      = onOW;

            string locStr = onOW ? PlaneStr(plane) + " surface" : "interior";
            string state  = inBat ? "BATTLE" : (trans ? "TRANS" : locStr);
            UpdateStatus($"Map:{mapId:X2}  X:{x}  Y:{y}  [{state}]" + (_pending != null ? "  [?]" : ""));
        }

        // ── Resolve pending — record OW entrance → destination ────────────────
        private void ResolvePending(string bannerName)
        {
            if (_pending == null) return;
            var p = _pending;
            _pending = null;

            string to = bannerName
                     ?? (_nameCache.TryGetValue(p.DestMap, out string c) ? c : null)
                     ?? $"Map_{p.DestMap:X2}";

            if (bannerName != null)
                _nameCache[p.DestMap] = bannerName;

            // Transit auto-detect
            if (!_planesSeen.TryGetValue(p.DestMap, out var planes))
                _planesSeen[p.DestMap] = planes = new HashSet<int>();
            planes.Add(p.DestPlane);
            if (planes.Count >= 2) { _transitMaps.Add(p.DestMap); return; }
            if (_transitMaps.Contains(p.DestMap)) return;

            // Deduplicate: key is "from|to" normally.
            // For multi-entrance locations (several OW tiles with the same name),
            // include the raw coord so each physical entrance is tracked separately.
            string dedupFrom = MultiEntrance.Contains(p.FromLabel)
                ? p.FromLabel + "@" + p.FromCoord
                : p.FromLabel;
            string key = dedupFrom + "|" + to;
            if (!_knownPairs.Add(key)) return;

            // Add to display
            string line = $"{p.FromLabel}  →  {to}";
            if (_panel.InvokeRequired)
                _panel.Invoke((Action)(() => { _panel.AddItem(line); _countLabel.Text = $"{_panel.ItemCount} connection(s) found"; }));
            else
                { _panel.AddItem(line); _countLabel.Text = $"{_panel.ItemCount} connection(s) found"; }
        }

        // ── UI construction ───────────────────────────────────────────────────
        private void BuildUI()
        {
            Text            = "FF4FE Entrance Randomizer Tracker";
            Size            = new Size(560, 420);
            MinimumSize     = new Size(360, 240);
            BackColor       = Color.FromArgb(18, 18, 24);
            ForeColor       = Color.FromArgb(220, 220, 235);
            Font            = new Font("Consolas", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.Manual;
            Location        = new Point(100, 100);

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(28, 28, 38) };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                ForeColor = Color.FromArgb(120, 210, 160),
                Font      = new Font("Consolas", 8.5f),
            };
            topPanel.Controls.Add(_statusLabel);

            _panel = new ConnectionPanel { Dock = DockStyle.Fill };

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.FromArgb(28, 28, 38) };
            _countLabel = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                ForeColor = Color.FromArgb(140, 140, 170),
                Font      = new Font("Consolas", 8.5f),
                Text      = "0 connection(s) found",
            };
            bottomPanel.Controls.Add(_countLabel);

            Controls.Add(_panel);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
        }

        private void UpdateStatus(string msg)
        {
            if (_statusLabel.InvokeRequired)
                _statusLabel.Invoke((Action)(() => _statusLabel.Text = msg));
            else
                _statusLabel.Text = msg;
        }
    }
}
