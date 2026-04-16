namespace BasketballScout.App.Views;

/// <summary>
/// Draws a FIBA-accurate basketball half-court with dark theme matching the V3 mockup.
/// Dark background with subtle golden-brown court lines.
///
/// Proportions (must match <c>PdfReportService.DrawCourt</c> so shot markers
/// saved as (CourtX, CourtY) appear in the same visual location on both app and PDF):
///   - 15m wide × 14m deep half-court
///   - Paint 4.9m × 5.8m
///   - Free-throw circle radius 1.8m
///   - Rim 0.46m diameter, 1.575m from baseline
///   - Backboard 1.8m wide, 1.2m from baseline
///   - 3PT arc radius 6.75m from basket, corner-3 lines 0.9m from sidelines
///   - Restricted area arc 1.25m radius
///
/// Orientation: basket at the BOTTOM of the drawn rectangle
/// (Y grows down, so "distance from baseline" maps to h - dist).
/// </summary>
public class CourtDrawable : IDrawable
{
    // FIBA official half-court dimensions (metres)
    private const float CourtWidthM = 15.0f;
    private const float CourtDepthM = 14.0f;
    private const float PaintWidthM = 4.9f;
    private const float PaintDepthM = 5.8f;
    private const float FtCircleRM = 1.8f;
    private const float RimDistFromBaselineM = 1.575f;
    private const float RimDiameterM = 0.46f;
    private const float BackboardDistFromBaselineM = 1.2f;
    private const float BackboardWidthM = 1.8f;
    private const float ThreePtRadiusM = 6.75f;
    private const float CornerThreeInsetM = 0.9f;
    private const float RestrictedAreaRM = 1.25f;
    private const float CenterCircleRM = 1.8f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float x = dirtyRect.X;
        float y = dirtyRect.Y;
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;

        // Background — dark court
        canvas.FillColor = Color.FromArgb("#18150f");
        canvas.FillRectangle(x, y, w, h);

        // Palette
        var lineColor = Color.FromArgb("#3a352e");
        var paintLine = Color.FromArgb("#2a2520");
        var faintLine = Color.FromArgb("#2a252088");
        var paintFill = Color.FromArgb("#1e1a1444");
        var rimColor = Color.FromArgb("#888888");
        var backboardColor = Color.FromArgb("#555555");
        var textColor = Color.FromArgb("#2a2520");

        // Per-metre scale
        float sx = w / CourtWidthM;
        float sy = h / CourtDepthM;

        // Basket / baseline reference points (basket is at the BOTTOM).
        float basketX = x + w * 0.5f;
        float basketY = y + h - RimDistFromBaselineM * sy;
        float bbY = y + h - BackboardDistFromBaselineM * sy;
        float bbHalfW = BackboardWidthM * 0.5f * sx;

        // ── Outer boundary ──
        canvas.StrokeColor = paintLine;
        canvas.StrokeSize = 1.2f;
        canvas.DrawRectangle(x, y, w, h);

        // ── Paint (rectangular key) ──
        // Top edge = free-throw line, bottom edge = baseline.
        float paintHalfW = PaintWidthM * 0.5f * sx;
        float paintX = basketX - paintHalfW;
        float paintW = paintHalfW * 2f;
        float paintH = PaintDepthM * sy;
        float paintY = y + h - paintH;

        canvas.FillColor = paintFill;
        canvas.FillRectangle(paintX, paintY, paintW, paintH);

        canvas.StrokeColor = paintLine;
        canvas.StrokeSize = 1.2f;
        canvas.DrawRectangle(paintX, paintY, paintW, paintH);

        // ── Free-throw circle (radius 1.8m), centred on the FT line ──
        // Lower half (inside paint, toward basket) = solid.
        // Upper half (outside paint, toward centre court) = dashed.
        float ftCX = basketX;
        float ftCY = paintY;
        float ftRx = FtCircleRM * sx;
        float ftRy = FtCircleRM * sy;

        // In Y-down, angles 0→π sweep through the bottom of the ellipse (inside paint).
        DrawPolylineArc(canvas, paintLine, 1.2f, null, ftCX, ftCY, ftRx, ftRy, 0f, MathF.PI);
        // Upper half (outside paint): angles π→2π, dashed.
        DrawPolylineArc(canvas, faintLine, 0.8f, new float[] { 4f, 4f },
                        ftCX, ftCY, ftRx, ftRy, MathF.PI, 2f * MathF.PI);

        // ── 3-point line (corner straights + arc) ──
        float arcRx = ThreePtRadiusM * sx;
        float arcRy = ThreePtRadiusM * sy;
        float cornerInset = CornerThreeInsetM * sx;
        float leftCornerX = x + cornerInset;
        float rightCornerX = x + w - cornerInset;

        // Find where the arc meets each corner line.
        // (dx/arcRx)² + (dy/arcRy)² = 1  ⇒  dy = arcRy · √(1 − (dx/arcRx)²)
        // Arc bulges UP (toward centre court), so dy is NEGATIVE in Y-down.
        float dxCorner = (leftCornerX - basketX) / arcRx;
        float dyMag = arcRy * MathF.Sqrt(MathF.Max(0f, 1f - dxCorner * dxCorner));
        float cornerEndY = basketY - dyMag; // above basketY in Y-down

        // Corner-3 straight lines: from baseline (bottom) up to where the arc meets them.
        canvas.StrokeColor = lineColor;
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(leftCornerX, y + h, leftCornerX, cornerEndY);
        canvas.DrawLine(rightCornerX, y + h, rightCornerX, cornerEndY);

        // Arc: right corner → top → left corner.
        // startAngle = atan2((cornerEndY - basketY)/arcRy, (rightCornerX - basketX)/arcRx)
        //   dy is negative, dx is positive ⇒ small negative angle.
        // endAngle = -π - startAngle (mirrored on the left side, still negative).
        float startAngle = MathF.Atan2(
            (cornerEndY - basketY) / arcRy,
            (rightCornerX - basketX) / arcRx);
        float endAngle = -MathF.PI - startAngle;
        DrawPolylineArc(canvas, lineColor, 1.5f, null,
                        basketX, basketY, arcRx, arcRy, startAngle, endAngle);

        // ── Restricted area (1.25m no-charge semi-circle) ──
        // Upper half of the ellipse (bulges UP toward centre court): angles -π → 0.
        float raRx = RestrictedAreaRM * sx;
        float raRy = RestrictedAreaRM * sy;
        DrawPolylineArc(canvas, faintLine, 0.8f, null,
                        basketX, basketY, raRx, raRy, -MathF.PI, 0f);

        // ── Backboard — thick line, closer to baseline than the rim ──
        canvas.StrokeColor = backboardColor;
        canvas.StrokeSize = 2f;
        canvas.DrawLine(basketX - bbHalfW, bbY, basketX + bbHalfW, bbY);

        // ── Rim — circle of 0.46m diameter at basket position ──
        canvas.StrokeColor = rimColor;
        canvas.StrokeSize = 1.5f;
        float rimRx = RimDiameterM * 0.5f * sx;
        float rimRy = RimDiameterM * 0.5f * sy;
        float rimR = MathF.Max(rimRx, MathF.Max(rimRy, 2f));
        canvas.DrawEllipse(basketX - rimR, basketY - rimR, rimR * 2f, rimR * 2f);

        // Small stem from backboard to rim so the basket reads clearly.
        canvas.StrokeColor = paintLine;
        canvas.StrokeSize = 1f;
        canvas.DrawLine(basketX, bbY, basketX, basketY - rimR);

        // ── Centre-circle half at the far (top) end of the court ──
        // On a half-court view the centre line is at the top, and we see only our
        // half of the centre circle (the half on this side of the centre line).
        float centerR = CenterCircleRM * sx;
        DrawPolylineArc(canvas, faintLine, 0.8f, null,
                        basketX, y, centerR, CenterCircleRM * sy, 0f, MathF.PI);

        // ── Text labels ──
        canvas.FontColor = textColor;
        canvas.FontSize = w * 0.035f;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.DrawString("3PT ZONE", x, y + h * 0.04f, w, h * 0.06f,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.FontColor = faintLine;
        canvas.FontSize = w * 0.03f;
        canvas.DrawString("PAINT", paintX, paintY + paintH * 0.25f,
            paintW, paintH * 0.3f,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    /// <summary>
    /// Approximates an elliptical arc as a polyline.
    /// Angles in radians, standard math convention (0 = +X, +π/2 = +Y).
    /// Because MAUI's Y grows down, +π/2 draws BELOW the centre.
    /// </summary>
    private static void DrawPolylineArc(
        ICanvas canvas, Color color, float strokeWidth, float[]? dashPattern,
        float cx, float cy, float rx, float ry,
        float startAngle, float endAngle, int segments = 64)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = strokeWidth;
        canvas.StrokeDashPattern = dashPattern;

        var path = new PathF();
        for (int i = 0; i <= segments; i++)
        {
            float t = startAngle + (endAngle - startAngle) * i / segments;
            float px = cx + rx * MathF.Cos(t);
            float py = cy + ry * MathF.Sin(t);
            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }
        canvas.DrawPath(path);
        canvas.StrokeDashPattern = null;
    }
}
