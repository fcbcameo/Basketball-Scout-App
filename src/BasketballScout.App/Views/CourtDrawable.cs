namespace BasketballScout.App.Views;

/// <summary>
/// Draws a basketball half-court with dark theme matching the V3 mockup.
/// Dark background with subtle golden-brown court lines.
/// </summary>
public class CourtDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;

        // Background — dark court
        canvas.FillColor = Color.FromArgb("#18150f");
        canvas.FillRectangle(0, 0, w, h);

        // Court line colors
        var lineColor = Color.FromArgb("#2a2520");
        var faintLineColor = Color.FromArgb("#2a252088");
        var rimColor = Color.FromArgb("#888888");
        var backboardColor = Color.FromArgb("#555555");
        var textColor = Color.FromArgb("#2a2520");

        canvas.StrokeSize = 1.5f;
        canvas.StrokeColor = lineColor;

        // ── Outer boundary ──
        float margin = w * 0.02f;
        canvas.DrawRectangle(margin, margin, w - 2 * margin, h - 2 * margin);

        // ── Half court line (top) ──
        canvas.StrokeSize = 1.5f;
        canvas.StrokeColor = lineColor;
        canvas.DrawLine(margin, margin, w - margin, margin);

        // ── Paint / Key (rectangle near basket) ──
        // Paint is centered, extends from baseline (bottom) up ~38% of court
        float paintLeft = w * 0.31f;
        float paintRight = w * 0.69f;
        float paintTop = h * 0.60f;
        float paintBottom = h - margin;
        float paintWidth = paintRight - paintLeft;
        float paintHeight = paintBottom - paintTop;

        canvas.StrokeSize = 1.2f;
        canvas.StrokeColor = lineColor;

        // Paint fill (subtle)
        canvas.FillColor = Color.FromArgb("#1e1a1444");
        canvas.FillRectangle(paintLeft, paintTop, paintWidth, paintHeight);

        // Paint outline
        canvas.DrawRectangle(paintLeft, paintTop, paintWidth, paintHeight);

        // Free throw line
        canvas.DrawLine(paintLeft, paintTop, paintRight, paintTop);

        // Free throw circle (dashed)
        canvas.StrokeColor = faintLineColor;
        canvas.StrokeSize = 0.8f;
        canvas.StrokeDashPattern = new float[] { 4, 4 };
        float ftCircleRadius = w * 0.12f;
        canvas.DrawEllipse(w * 0.5f - ftCircleRadius, paintTop - ftCircleRadius,
            ftCircleRadius * 2, ftCircleRadius * 2);
        canvas.StrokeDashPattern = null;

        // ── 3-Point arc ──
        canvas.StrokeSize = 1.5f;
        canvas.StrokeColor = Color.FromArgb("#3a352e");

        // 3pt arc — draw as a path
        // The arc goes from the left corner to the right corner, curving through the top
        float arcCenterX = w * 0.5f;
        float arcCenterY = h * 0.98f; // Near baseline
        float arcRadius = w * 0.42f;

        // Left straight portion (corner 3)
        float cornerEndY = h * 0.58f;
        canvas.DrawLine(w * 0.10f, h - margin, w * 0.10f, cornerEndY);

        // Right straight portion (corner 3)
        canvas.DrawLine(w * 0.90f, h - margin, w * 0.90f, cornerEndY);

        // Arc portion — approximate with a bezier-like path
        var path = new PathF();
        path.MoveTo(w * 0.10f, cornerEndY);

        // Quadratic bezier through the top
        // Control points to make a nice arc
        path.QuadTo(w * 0.10f, h * 0.20f, w * 0.50f, h * 0.16f);
        canvas.DrawPath(path);

        var path2 = new PathF();
        path2.MoveTo(w * 0.50f, h * 0.16f);
        path2.QuadTo(w * 0.90f, h * 0.20f, w * 0.90f, cornerEndY);
        canvas.DrawPath(path2);

        // ── Restricted area / Charge circle ──
        canvas.StrokeColor = faintLineColor;
        canvas.StrokeSize = 0.8f;
        var restrictedPath = new PathF();
        restrictedPath.MoveTo(w * 0.44f, h - margin);
        restrictedPath.QuadTo(w * 0.44f, h * 0.82f, w * 0.50f, h * 0.80f);
        restrictedPath.QuadTo(w * 0.56f, h * 0.82f, w * 0.56f, h - margin);
        canvas.DrawPath(restrictedPath);

        // ── Backboard ──
        canvas.StrokeColor = backboardColor;
        canvas.StrokeSize = 2f;
        canvas.DrawLine(w * 0.43f, h * 0.93f, w * 0.57f, h * 0.93f);

        // ── Rim ──
        canvas.StrokeColor = rimColor;
        canvas.StrokeSize = 1.5f;
        float rimRadius = w * 0.018f;
        canvas.DrawEllipse(w * 0.5f - rimRadius, h * 0.95f - rimRadius,
            rimRadius * 2, rimRadius * 2);

        // ── Center court half-circle (at top) ──
        canvas.StrokeColor = faintLineColor;
        canvas.StrokeSize = 0.8f;
        var topArc = new PathF();
        topArc.MoveTo(w * 0.38f, margin);
        topArc.QuadTo(w * 0.38f, h * 0.14f, w * 0.50f, h * 0.14f);
        topArc.QuadTo(w * 0.62f, h * 0.14f, w * 0.62f, margin);
        canvas.DrawPath(topArc);

        // ── Text labels ──
        canvas.FontColor = textColor;
        canvas.FontSize = w * 0.035f;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;

        // "3PT ZONE" text near top
        canvas.DrawString("3PT ZONE", 0, h * 0.06f, w, h * 0.06f,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        // "PAINT" text in the paint area
        canvas.FontColor = faintLineColor;
        canvas.FontSize = w * 0.03f;
        canvas.DrawString("PAINT", 0, paintTop + paintHeight * 0.3f, w, paintHeight * 0.3f,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
