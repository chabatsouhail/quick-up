using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace QuickUp;

internal enum TablerIcon
{
    ChevronLeft,
    ChevronRight,
    Home,
    Refresh,
    ExternalLink,
    Pin,
    Pinned,
    Settings,
    World,
    Book,
    MessageCircle,
    Code,
    LayoutKanban,
    Star,
    Calendar,
    AdjustmentsHorizontal,
    Palette,
    AppWindow,
    Link,
    Sun,
    Moon,
    Upload,
    Restore,
    Bolt
}

/// <summary>
/// Zero-dependency GDI+ renderer for official Tabler v3 outline SVG icons (24x24 viewBox).
/// </summary>
internal static class TablerIcons
{
    private static readonly ConcurrentDictionary<TablerIcon, GraphicsPath> PathCache = new();

    private static string[] GetPaths(TablerIcon icon) => icon switch
    {
        TablerIcon.ChevronLeft => ["M15 6l-6 6l6 6"],
        TablerIcon.ChevronRight => ["M9 6l6 6l-6 6"],
        TablerIcon.Home =>
        [
            "M5 12l-2 0l9 -9l9 9l-2 0",
            "M5 12v7a2 2 0 0 0 2 2h10a2 2 0 0 0 2 -2v-7",
            "M9 21v-6a2 2 0 0 1 2 -2h2a2 2 0 0 1 2 2v6"
        ],
        TablerIcon.Refresh =>
        [
            "M20 11a8.1 8.1 0 0 0 -15.5 -2m-.5 -4v4h4",
            "M4 13a8.1 8.1 0 0 0 15.5 2m.5 4v-4h-4"
        ],
        TablerIcon.ExternalLink =>
        [
            "M12 6h-6a2 2 0 0 0 -2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2 -2v-6",
            "M11 13l9 -9",
            "M15 4h5v5"
        ],
        TablerIcon.Pin =>
        [
            "M15 4.5l-4 4l-4 1.5l-1.5 1.5l7 7l1.5 -1.5l1.5 -4l4 -4",
            "M9 15l-4.5 4.5",
            "M14.5 4l5.5 5.5"
        ],
        TablerIcon.Pinned =>
        [
            "M9 4v6l-2 4v2h10v-2l-2 -4v-6",
            "M12 16l0 5",
            "M8 4l8 0"
        ],
        TablerIcon.Settings =>
        [
            "M10.325 4.317c.426 -1.756 2.924 -1.756 3.35 0a1.724 1.724 0 0 0 2.573 1.066c1.543 -.94 3.31 .826 2.37 2.37a1.724 1.724 0 0 0 1.065 2.572c1.756 .426 1.756 2.924 0 3.35a1.724 1.724 0 0 0 -1.066 2.573c.94 1.543 -.826 3.31 -2.37 2.37a1.724 1.724 0 0 0 -2.572 1.065c-.426 1.756 -2.924 1.756 -3.35 0a1.724 1.724 0 0 0 -2.573 -1.066c-1.543 .94 -3.31 -.826 -2.37 -2.37a1.724 1.724 0 0 0 -1.065 -2.572c-1.756 -.426 -1.756 -2.924 0 -3.35a1.724 1.724 0 0 0 1.066 -2.573c-.94 -1.543 .826 -3.31 2.37 -2.37c1 .608 2.296 .07 2.572 -1.065",
            "M9 12a3 3 0 1 0 6 0a3 3 0 0 0 -6 0"
        ],
        TablerIcon.World =>
        [
            "M3 12a9 9 0 1 0 18 0a9 9 0 0 0 -18 0",
            "M3.6 9h16.8",
            "M3.6 15h16.8",
            "M11.5 3a17 17 0 0 0 0 18",
            "M12.5 3a17 17 0 0 1 0 18"
        ],
        TablerIcon.Book =>
        [
            "M3 19a9 9 0 0 1 9 0a9 9 0 0 1 9 0",
            "M3 6a9 9 0 0 1 9 0a9 9 0 0 1 9 0",
            "M3 6l0 13",
            "M12 6l0 13",
            "M21 6l0 13"
        ],
        TablerIcon.MessageCircle =>
        [
            "M3 20l1.3 -3.9c-2.324 -3.437 -1.426 -7.872 2.1 -10.374c3.526 -2.501 8.59 -2.296 11.845 .48c3.255 2.777 3.695 7.266 1.029 10.501c-2.666 3.235 -7.615 4.215 -11.574 2.293l-4.7 1"
        ],
        TablerIcon.Code =>
        [
            "M7 8l-4 4l4 4",
            "M17 8l4 4l-4 4",
            "M14 4l-4 16"
        ],
        TablerIcon.LayoutKanban =>
        [
            "M4 4l6 0",
            "M14 4l6 0",
            "M4 10a2 2 0 0 1 2 -2h2a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-2a2 2 0 0 1 -2 -2l0 -8",
            "M14 10a2 2 0 0 1 2 -2h2a2 2 0 0 1 2 2v2a2 2 0 0 1 -2 2h-2a2 2 0 0 1 -2 -2l0 -2"
        ],
        TablerIcon.Star =>
        [
            "M12 17.75l-6.172 3.245l1.179 -6.873l-5 -4.867l6.9 -1l3.086 -6.253l3.086 6.253l6.9 1l-5 4.867l1.179 6.873l-6.158 -3.245"
        ],
        TablerIcon.Calendar =>
        [
            "M4 7a2 2 0 0 1 2 -2h12a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-12",
            "M16 3v4",
            "M8 3v4",
            "M4 11h16"
        ],
        TablerIcon.AdjustmentsHorizontal =>
        [
            "M12 6a2 2 0 1 0 4 0a2 2 0 1 0 -4 0",
            "M4 6l8 0",
            "M16 6l4 0",
            "M6 12a2 2 0 1 0 4 0a2 2 0 1 0 -4 0",
            "M4 12l2 0",
            "M10 12l10 0",
            "M15 18a2 2 0 1 0 4 0a2 2 0 1 0 -4 0",
            "M4 18l11 0",
            "M19 18l1 0"
        ],
        TablerIcon.Palette =>
        [
            "M12 21a9 9 0 0 1 0 -18c4.97 0 9 3.582 9 8c0 1.06 -.474 2.078 -1.318 2.828c-.844 .75 -1.989 1.172 -3.182 1.172h-2.5a2 2 0 0 0 -1 3.75a1.3 1.3 0 0 1 -1 2.25",
            "M7.5 10.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0",
            "M11.5 7.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0",
            "M15.5 10.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0"
        ],
        TablerIcon.AppWindow =>
        [
            "M3 7a2 2 0 0 1 2 -2h14a2 2 0 0 1 2 2v10a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-10",
            "M6 8h.01",
            "M9 8h.01"
        ],
        TablerIcon.Link =>
        [
            "M9 15l6 -6",
            "M11 6l.463 -.536a5 5 0 0 1 7.071 7.072l-.534 .464",
            "M13 18l-.397 .534a5.068 5.068 0 0 1 -7.127 0a4.972 4.972 0 0 1 0 -7.071l.524 -.463"
        ],
        TablerIcon.Sun =>
        [
            "M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0",
            "M3 12h1m8 -9v1m8 8h1m-9 8v1m-6.4 -15.4l.7 .7m12.1 -.7l-.7 .7m0 11.4l.7 .7m-12.1 -.7l-.7 .7"
        ],
        TablerIcon.Moon =>
        [
            "M12 3c.132 0 .263 0 .393 0a7.5 7.5 0 0 0 7.92 12.446a9 9 0 1 1 -8.313 -12.454l0 .008"
        ],
        TablerIcon.Upload =>
        [
            "M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2v-2",
            "M7 9l5 -5l5 5",
            "M12 4l0 12"
        ],
        TablerIcon.Restore =>
        [
            "M3.06 13a9 9 0 1 0 .49 -4.087",
            "M3 4.001v5h5",
            "M11 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0"
        ],
        TablerIcon.Bolt => ["M13 3l0 7l6 0l-8 11l0 -7l-6 0l8 -11"],
        _ => []
    };

    public static void Draw(Graphics g, TablerIcon icon, float x, float y, float size, Color color, float strokeWidth24 = 2.0f)
    {
        float scale = size / 24f;
        if (scale <= 0f) return;

        GraphicsPath path = PathCache.GetOrAdd(icon, BuildPath);
        GraphicsState state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(x, y);
            g.ScaleTransform(scale, scale);

            using var pen = new Pen(color, strokeWidth24)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawPath(pen, path);
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static GraphicsPath BuildPath(TablerIcon icon)
    {
        var path = new GraphicsPath();
        foreach (string d in GetPaths(icon))
        {
            AppendSvgPath(path, d);
        }
        return path;
    }

    private static void AppendSvgPath(GraphicsPath path, string d)
    {
        var parser = new SvgPathParser(d);
        float curX = 0f;
        float curY = 0f;
        float startX = 0f;
        float startY = 0f;
        float lastCtrlX = 0f;
        float lastCtrlY = 0f;
        char lastCmd = '\0';

        while (parser.TryReadCommandOrContinue(lastCmd, out char cmd, out bool implicitRepeat))
        {
            bool rel = char.IsLower(cmd);
            char upper = char.ToUpperInvariant(cmd);

            switch (upper)
            {
                case 'M':
                {
                    if (implicitRepeat)
                    {
                        float lx = parser.ReadFloat();
                        float ly = parser.ReadFloat();
                        if (rel) { lx += curX; ly += curY; }
                        path.AddLine(curX, curY, lx, ly);
                        curX = lx;
                        curY = ly;
                    }
                    else
                    {
                        float mx = parser.ReadFloat();
                        float my = parser.ReadFloat();
                        if (rel) { mx += curX; my += curY; }
                        path.StartFigure();
                        curX = mx;
                        curY = my;
                        startX = mx;
                        startY = my;
                    }
                    lastCtrlX = curX;
                    lastCtrlY = curY;
                    break;
                }

                case 'L':
                {
                    float lx = parser.ReadFloat();
                    float ly = parser.ReadFloat();
                    if (rel) { lx += curX; ly += curY; }
                    path.AddLine(curX, curY, lx, ly);
                    curX = lx;
                    curY = ly;
                    lastCtrlX = curX;
                    lastCtrlY = curY;
                    break;
                }

                case 'H':
                {
                    float hx = parser.ReadFloat();
                    if (rel) hx += curX;
                    path.AddLine(curX, curY, hx, curY);
                    curX = hx;
                    lastCtrlX = curX;
                    lastCtrlY = curY;
                    break;
                }

                case 'V':
                {
                    float vy = parser.ReadFloat();
                    if (rel) vy += curY;
                    path.AddLine(curX, curY, curX, vy);
                    curY = vy;
                    lastCtrlX = curX;
                    lastCtrlY = curY;
                    break;
                }

                case 'C':
                {
                    float x1 = parser.ReadFloat();
                    float y1 = parser.ReadFloat();
                    float x2 = parser.ReadFloat();
                    float y2 = parser.ReadFloat();
                    float x = parser.ReadFloat();
                    float y = parser.ReadFloat();
                    if (rel)
                    {
                        x1 += curX; y1 += curY;
                        x2 += curX; y2 += curY;
                        x += curX; y += curY;
                    }
                    path.AddBezier(curX, curY, x1, y1, x2, y2, x, y);
                    lastCtrlX = x2;
                    lastCtrlY = y2;
                    curX = x;
                    curY = y;
                    break;
                }

                case 'S':
                {
                    float x1 = (lastCmd is 'C' or 'c' or 'S' or 's') ? 2f * curX - lastCtrlX : curX;
                    float y1 = (lastCmd is 'C' or 'c' or 'S' or 's') ? 2f * curY - lastCtrlY : curY;
                    float x2 = parser.ReadFloat();
                    float y2 = parser.ReadFloat();
                    float x = parser.ReadFloat();
                    float y = parser.ReadFloat();
                    if (rel)
                    {
                        x2 += curX; y2 += curY;
                        x += curX; y += curY;
                    }
                    path.AddBezier(curX, curY, x1, y1, x2, y2, x, y);
                    lastCtrlX = x2;
                    lastCtrlY = y2;
                    curX = x;
                    curY = y;
                    break;
                }

                case 'Q':
                {
                    float qx = parser.ReadFloat();
                    float qy = parser.ReadFloat();
                    float x = parser.ReadFloat();
                    float y = parser.ReadFloat();
                    if (rel)
                    {
                        qx += curX; qy += curY;
                        x += curX; y += curY;
                    }
                    float c1x = curX + (2f / 3f) * (qx - curX);
                    float c1y = curY + (2f / 3f) * (qy - curY);
                    float c2x = x + (2f / 3f) * (qx - x);
                    float c2y = y + (2f / 3f) * (qy - y);
                    path.AddBezier(curX, curY, c1x, c1y, c2x, c2y, x, y);
                    lastCtrlX = qx;
                    lastCtrlY = qy;
                    curX = x;
                    curY = y;
                    break;
                }

                case 'T':
                {
                    float qx = (lastCmd is 'Q' or 'q' or 'T' or 't') ? 2f * curX - lastCtrlX : curX;
                    float qy = (lastCmd is 'Q' or 'q' or 'T' or 't') ? 2f * curY - lastCtrlY : curY;
                    float x = parser.ReadFloat();
                    float y = parser.ReadFloat();
                    if (rel) { x += curX; y += curY; }
                    float c1x = curX + (2f / 3f) * (qx - curX);
                    float c1y = curY + (2f / 3f) * (qy - curY);
                    float c2x = x + (2f / 3f) * (qx - x);
                    float c2y = y + (2f / 3f) * (qy - y);
                    path.AddBezier(curX, curY, c1x, c1y, c2x, c2y, x, y);
                    lastCtrlX = qx;
                    lastCtrlY = qy;
                    curX = x;
                    curY = y;
                    break;
                }

                case 'A':
                {
                    float rx = parser.ReadFloat();
                    float ry = parser.ReadFloat();
                    float xAxisRotation = parser.ReadFloat();
                    bool largeArc = parser.ReadFlag();
                    bool sweep = parser.ReadFlag();
                    float x = parser.ReadFloat();
                    float y = parser.ReadFloat();
                    if (rel) { x += curX; y += curY; }
                    AppendArc(path, curX, curY, rx, ry, xAxisRotation, largeArc, sweep, x, y);
                    curX = x;
                    curY = y;
                    lastCtrlX = curX;
                    lastCtrlY = curY;
                    break;
                }

                case 'Z':
                {
                    path.CloseFigure();
                    curX = startX;
                    curY = startY;
                    lastCtrlX = curX;
                    lastCtrlY = curY;
                    break;
                }
            }

            lastCmd = cmd;
        }
    }

    private static void AppendArc(
        GraphicsPath path,
        double x1, double y1,
        double rx, double ry,
        double phiDeg,
        bool largeArc, bool sweep,
        double x2, double y2)
    {
        if (Math.Abs(x1 - x2) < 1e-6 && Math.Abs(y1 - y2) < 1e-6) return;

        if (rx == 0 || ry == 0)
        {
            path.AddLine((float)x1, (float)y1, (float)x2, (float)y2);
            return;
        }

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);

        double phi = phiDeg * Math.PI / 180.0;
        double cosPhi = Math.Cos(phi);
        double sinPhi = Math.Sin(phi);

        double dx2 = (x1 - x2) / 2.0;
        double dy2 = (y1 - y2) / 2.0;
        double x1p = cosPhi * dx2 + sinPhi * dy2;
        double y1p = -sinPhi * dx2 + cosPhi * dy2;

        double lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
        if (lambda > 1.0)
        {
            double scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        double rx2 = rx * rx;
        double ry2 = ry * ry;
        double x1p2 = x1p * x1p;
        double y1p2 = y1p * y1p;

        double num = Math.Max(0.0, rx2 * ry2 - rx2 * y1p2 - ry2 * x1p2);
        double den = rx2 * y1p2 + ry2 * x1p2;
        double coef = (largeArc == sweep ? -1.0 : 1.0) * Math.Sqrt(den == 0 ? 0 : num / den);

        double cxp = coef * (rx * y1p / ry);
        double cyp = coef * -(ry * x1p / rx);

        double cx = cosPhi * cxp - sinPhi * cyp + (x1 + x2) / 2.0;
        double cy = sinPhi * cxp + cosPhi * cyp + (y1 + y2) / 2.0;

        double ux = (x1p - cxp) / rx;
        double uy = (y1p - cyp) / ry;
        double vx = (-x1p - cxp) / rx;
        double vy = (-y1p - cyp) / ry;

        double theta1 = VectorAngle(1.0, 0.0, ux, uy);
        double dTheta = VectorAngle(ux, uy, vx, vy);

        if (!sweep && dTheta > 0) dTheta -= 2.0 * Math.PI;
        if (sweep && dTheta < 0) dTheta += 2.0 * Math.PI;

        int segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(dTheta) / (Math.PI / 2.0)));
        double delta = dTheta / segments;

        for (int i = 0; i < segments; i++)
        {
            double t1 = theta1 + i * delta;
            double t2 = t1 + delta;
            double alpha = (4.0 / 3.0) * Math.Tan((t2 - t1) / 4.0);

            double p1x = Math.Cos(t1);
            double p1y = Math.Sin(t1);
            double p2x = Math.Cos(t2);
            double p2y = Math.Sin(t2);

            double q1x = p1x - alpha * p1y;
            double q1y = p1y + alpha * p1x;
            double q2x = p2x + alpha * p2y;
            double q2y = p2y - alpha * p2x;

            PointF b0 = MapUnitPoint(p1x, p1y, rx, ry, cosPhi, sinPhi, cx, cy);
            PointF b1 = MapUnitPoint(q1x, q1y, rx, ry, cosPhi, sinPhi, cx, cy);
            PointF b2 = MapUnitPoint(q2x, q2y, rx, ry, cosPhi, sinPhi, cx, cy);
            PointF b3 = MapUnitPoint(p2x, p2y, rx, ry, cosPhi, sinPhi, cx, cy);

            path.AddBezier(b0, b1, b2, b3);
        }
    }

    private static PointF MapUnitPoint(
        double ux, double uy,
        double rx, double ry,
        double cosPhi, double sinPhi,
        double cx, double cy)
    {
        double sx = ux * rx;
        double sy = uy * ry;
        return new PointF(
            (float)(cosPhi * sx - sinPhi * sy + cx),
            (float)(sinPhi * sx + cosPhi * sy + cy));
    }

    private static double VectorAngle(double ux, double uy, double vx, double vy)
    {
        double dot = ux * vx + uy * vy;
        double len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        double ratio = len == 0 ? 1.0 : Math.Clamp(dot / len, -1.0, 1.0);
        double sign = (ux * vy - uy * vx < 0) ? -1.0 : 1.0;
        return sign * Math.Acos(ratio);
    }

    private ref struct SvgPathParser
    {
        private readonly string _src;
        private int _pos;

        public SvgPathParser(string src)
        {
            _src = src;
            _pos = 0;
        }

        public bool TryReadCommandOrContinue(char lastCmd, out char cmd, out bool implicitRepeat)
        {
            SkipSeparators();
            if (_pos >= _src.Length)
            {
                cmd = '\0';
                implicitRepeat = false;
                return false;
            }

            char c = _src[_pos];
            if (char.IsLetter(c))
            {
                _pos++;
                cmd = c;
                implicitRepeat = false;
                return true;
            }

            if (lastCmd != '\0' && lastCmd is not ('Z' or 'z'))
            {
                cmd = lastCmd;
                implicitRepeat = true;
                return true;
            }

            cmd = '\0';
            implicitRepeat = false;
            return false;
        }

        public float ReadFloat()
        {
            SkipSeparators();
            int start = _pos;
            if (_pos < _src.Length && (_src[_pos] == '+' || _src[_pos] == '-'))
            {
                _pos++;
            }

            bool seenDot = false;
            while (_pos < _src.Length)
            {
                char ch = _src[_pos];
                if (char.IsDigit(ch))
                {
                    _pos++;
                }
                else if (ch == '.' && !seenDot)
                {
                    seenDot = true;
                    _pos++;
                }
                else if (ch is 'e' or 'E')
                {
                    _pos++;
                    if (_pos < _src.Length && _src[_pos] is '+' or '-')
                    {
                        _pos++;
                    }
                }
                else
                {
                    break;
                }
            }

            return float.Parse(_src.AsSpan(start, _pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public bool ReadFlag()
        {
            SkipSeparators();
            if (_pos < _src.Length)
            {
                char c = _src[_pos++];
                return c == '1';
            }
            return false;
        }

        private void SkipSeparators()
        {
            while (_pos < _src.Length && (char.IsWhiteSpace(_src[_pos]) || _src[_pos] == ','))
            {
                _pos++;
            }
        }
    }
}
