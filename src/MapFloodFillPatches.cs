using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace Sts2ModTemplate;

[HarmonyPatch(typeof(NMapScreen), "_Ready")]
internal static class MapFloodFillButtonPatch
{
    private const string FillButtonName = "CodexMapFloodFillButton";
    private const float ButtonHalfWidth = 112f;
    private const float ButtonHeight = 46f;
    private const float ButtonBottomMargin = 22f;
    private const float FillInset = 4f;
    private const float FillLineGap = 4f;
    private const float VerticalFillLineGap = 6f;
    private const float PointFillGap = 6f;
    private const float MinPointScribbleRadius = 26f;
    private const float PointScribbleScale = 0.75f;

    private static readonly Color ButtonTextColor = new(0.97f, 0.96f, 0.92f, 1f);
    private static readonly Color ButtonOutlineColor = new(0.15f, 0.12f, 0.08f, 0.85f);

    private readonly record struct CircleMask(Vector2 Center, float Radius);
    private readonly record struct FloatSegment(float Start, float End);

    private static void Postfix(NMapScreen __instance)
    {
        TryAddFillButton(__instance);
    }

    private static void TryAddFillButton(NMapScreen screen)
    {
        if (screen is not Control screenControl)
        {
            return;
        }

        if (screenControl.GetNodeOrNull<Button>(FillButtonName) != null)
        {
            return;
        }

        Button button = BuildFillButton(screen);
        button.Pressed += () =>
        {
            button.Disabled = true;
            try
            {
                PaintWholeMap(screen);
            }
            finally
            {
                if (GodotObject.IsInstanceValid(button))
                {
                    button.Disabled = false;
                }
            }
        };

        screenControl.AddChild(button);
    }

    private static Button BuildFillButton(NMapScreen screen)
    {
        Button button = new()
        {
            Name = FillButtonName,
            Text = "Paint Map",
            TooltipText = "Use the map brush to paint the whole map except the start and final boss.",
            CustomMinimumSize = new Vector2(ButtonHalfWidth * 2f, ButtonHeight),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            ExpandIcon = true,
            Alignment = HorizontalAlignment.Center,
            IconAlignment = HorizontalAlignment.Left,
            ZIndex = 32
        };

        button.AnchorLeft = 0.5f;
        button.AnchorRight = 0.5f;
        button.AnchorTop = 1f;
        button.AnchorBottom = 1f;
        button.OffsetLeft = -ButtonHalfWidth;
        button.OffsetTop = -(ButtonBottomMargin + ButtonHeight);
        button.OffsetRight = ButtonHalfWidth;
        button.OffsetBottom = -ButtonBottomMargin;
        button.GrowHorizontal = Control.GrowDirection.Both;
        button.GrowVertical = Control.GrowDirection.Begin;

        button.Icon = ResolveLegendIcon(screen);
        button.AddThemeColorOverride("font_color", ButtonTextColor);
        button.AddThemeColorOverride("font_focus_color", ButtonTextColor);
        button.AddThemeColorOverride("font_hover_color", ButtonTextColor);
        button.AddThemeColorOverride("font_hover_pressed_color", ButtonTextColor);
        button.AddThemeColorOverride("font_pressed_color", ButtonTextColor);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.75f, 0.74f, 0.7f, 0.75f));
        button.AddThemeColorOverride("font_outline_color", ButtonOutlineColor);
        button.AddThemeConstantOverride("outline_size", 1);
        button.AddThemeConstantOverride("h_separation", 10);
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.11f, 0.12f, 0.15f, 0.88f), new Color(0.66f, 0.69f, 0.73f, 0.42f)));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.16f, 0.17f, 0.21f, 0.94f), new Color(0.88f, 0.9f, 0.94f, 0.7f)));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.19f, 0.2f, 0.25f, 0.98f), new Color(0.94f, 0.89f, 0.72f, 0.85f)));
        button.AddThemeStyleboxOverride("focus", CreateButtonStyle(new Color(0.15f, 0.16f, 0.2f, 0.94f), new Color(0.94f, 0.89f, 0.72f, 0.95f)));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(new Color(0.09f, 0.09f, 0.11f, 0.75f), new Color(0.45f, 0.47f, 0.5f, 0.3f)));
        return button;
    }

    private static StyleBoxFlat CreateButtonStyle(Color background, Color border)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            ContentMarginBottom = 10,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 10,
            ShadowColor = new Color(0f, 0f, 0f, 0.24f),
            ShadowSize = 5
        };
        return style;
    }

    private static void PaintWholeMap(NMapScreen screen)
    {
        if (screen.Drawings is not NMapDrawings drawings || drawings is not CanvasItem drawingsCanvas)
        {
            Log.Warn($"{ModEntry.ModId}: map fill skipped because the drawing layer is unavailable.", 2);
            return;
        }

        Control? mapBackground = GetFieldValue<Control>(screen, "_mapBgContainer")
            ?? GetFieldValue<Control>(screen, "_mapContainer");
        if (mapBackground == null)
        {
            Log.Warn($"{ModEntry.ModId}: map fill skipped because the map background was not found.", 2);
            return;
        }

        Rect2 fillArea = mapBackground.GetGlobalRect();
        fillArea.Position += new Vector2(FillInset, FillInset);
        fillArea.Size -= new Vector2(FillInset * 2f, FillInset * 2f);
        if (fillArea.Size.X <= 0f || fillArea.Size.Y <= 0f)
        {
            return;
        }

        DrawingMode previousMode = drawings.GetLocalDrawingMode(false);
        try
        {
            drawings.SetDrawingModeLocal(DrawingMode.Drawing);
            DrawBackgroundFill(screen, drawings, drawingsCanvas, fillArea);
            DrawPointScribbles(screen, drawings, drawingsCanvas);
        }
        catch (Exception ex)
        {
            Log.Error($"{ModEntry.ModId}: failed to paint the map. {ex}", 2);
        }
        finally
        {
            drawings.StopLineLocal();
            drawings.SetDrawingModeLocal(previousMode);
            InvokeMethod(screen, "UpdateDrawingButtonStates");
        }
    }

    private static void DrawBackgroundFill(NMapScreen screen, NMapDrawings drawings, CanvasItem drawingsCanvas, Rect2 fillArea)
    {
        List<CircleMask> masks = BuildExclusionMasks(screen);
        float left = fillArea.Position.X;
        float right = fillArea.Position.X + fillArea.Size.X;
        float top = fillArea.Position.Y;
        float bottom = fillArea.Position.Y + fillArea.Size.Y;

        DrawHorizontalFill(drawings, drawingsCanvas, masks, left, right, top, bottom, 0f);
        DrawHorizontalFill(drawings, drawingsCanvas, masks, left, right, top, bottom, FillLineGap / 3f);
        DrawHorizontalFill(drawings, drawingsCanvas, masks, left, right, top, bottom, (FillLineGap * 2f) / 3f);
        DrawVerticalFill(drawings, drawingsCanvas, masks, left, right, top, bottom, 0f);
        DrawVerticalFill(drawings, drawingsCanvas, masks, left, right, top, bottom, VerticalFillLineGap * 0.5f);
    }

    private static void DrawHorizontalFill(
        NMapDrawings drawings,
        CanvasItem drawingsCanvas,
        IReadOnlyList<CircleMask> masks,
        float left,
        float right,
        float top,
        float bottom,
        float offset)
    {
        for (float y = top + offset; y <= bottom; y += FillLineGap)
        {
            foreach (FloatSegment segment in BuildHorizontalSegments(left, right, y, masks))
            {
                if (segment.End - segment.Start < FillLineGap)
                {
                    continue;
                }

                DrawStroke(drawings, drawingsCanvas, new Vector2(segment.Start, y), new Vector2(segment.End, y));
            }
        }
    }

    private static void DrawVerticalFill(
        NMapDrawings drawings,
        CanvasItem drawingsCanvas,
        IReadOnlyList<CircleMask> masks,
        float left,
        float right,
        float top,
        float bottom,
        float offset)
    {
        for (float x = left + offset; x <= right; x += VerticalFillLineGap)
        {
            foreach (FloatSegment segment in BuildVerticalSegments(top, bottom, x, masks))
            {
                if (segment.End - segment.Start < FillLineGap)
                {
                    continue;
                }

                DrawStroke(drawings, drawingsCanvas, new Vector2(x, segment.Start), new Vector2(x, segment.End));
            }
        }
    }

    private static void DrawPointScribbles(NMapScreen screen, NMapDrawings drawings, CanvasItem drawingsCanvas)
    {
        Dictionary<MapCoord, NMapPoint>? mapPointDictionary = GetFieldValue<Dictionary<MapCoord, NMapPoint>>(screen, "_mapPointDictionary");
        if (mapPointDictionary == null || mapPointDictionary.Count == 0)
        {
            return;
        }

        HashSet<Node> excludedNodes = [];
        AddExcludedNode(excludedNodes, GetFieldValue<Node>(screen, "_startingPointNode"));
        AddExcludedNode(excludedNodes, GetFieldValue<Node>(screen, "_bossPointNode"));
        AddExcludedNode(excludedNodes, GetFieldValue<Node>(screen, "_secondBossPointNode"));

        foreach (NMapPoint pointNode in mapPointDictionary.Values.Distinct())
        {
            if (excludedNodes.Contains(pointNode) || pointNode.Point == null || pointNode.Point.PointType == MapPointType.Boss)
            {
                continue;
            }

            if (!TryGetNodeCenter(pointNode, out Vector2 center))
            {
                continue;
            }

            float radius = MathF.Max(EstimateNodeRadius(pointNode) * PointScribbleScale, MinPointScribbleRadius);
            for (float y = -radius; y <= radius; y += PointFillGap)
            {
                DrawStroke(drawings, drawingsCanvas, center + new Vector2(-radius, y), center + new Vector2(radius, y));
            }

            for (float x = -radius; x <= radius; x += PointFillGap)
            {
                DrawStroke(drawings, drawingsCanvas, center + new Vector2(x, -radius), center + new Vector2(x, radius));
            }

            DrawStroke(drawings, drawingsCanvas, center + new Vector2(-radius, -radius), center + new Vector2(radius, radius));
            DrawStroke(drawings, drawingsCanvas, center + new Vector2(-radius, radius), center + new Vector2(radius, -radius));
        }
    }

    private static List<CircleMask> BuildExclusionMasks(NMapScreen screen)
    {
        List<CircleMask> masks = [];
        TryAddMask(masks, GetFieldValue<Node>(screen, "_startingPointNode"), 28f);
        TryAddMask(masks, GetFieldValue<Node>(screen, "_bossPointNode"), 42f);
        TryAddMask(masks, GetFieldValue<Node>(screen, "_secondBossPointNode"), 42f);
        return masks;
    }

    private static void TryAddMask(List<CircleMask> masks, Node? node, float extraRadius)
    {
        if (!TryGetNodeCenter(node, out Vector2 center))
        {
            return;
        }

        masks.Add(new CircleMask(center, EstimateNodeRadius(node) + extraRadius));
    }

    private static List<FloatSegment> BuildHorizontalSegments(float left, float right, float y, IReadOnlyList<CircleMask> masks)
    {
        return BuildSegments(left, right, y, masks, useYDistance: true);
    }

    private static List<FloatSegment> BuildVerticalSegments(float top, float bottom, float x, IReadOnlyList<CircleMask> masks)
    {
        return BuildSegments(top, bottom, x, masks, useYDistance: false);
    }

    private static List<FloatSegment> BuildSegments(float start, float end, float axisPosition, IReadOnlyList<CircleMask> masks, bool useYDistance)
    {
        List<FloatSegment> segments = [new FloatSegment(start, end)];
        foreach (CircleMask mask in masks)
        {
            float distance = MathF.Abs(axisPosition - (useYDistance ? mask.Center.Y : mask.Center.X));
            if (distance >= mask.Radius)
            {
                continue;
            }

            float intersection = MathF.Sqrt((mask.Radius * mask.Radius) - (distance * distance));
            float cutCenter = useYDistance ? mask.Center.X : mask.Center.Y;
            segments = SubtractRange(segments, cutCenter - intersection, cutCenter + intersection);
            if (segments.Count == 0)
            {
                break;
            }
        }

        return segments;
    }

    private static List<FloatSegment> SubtractRange(IEnumerable<FloatSegment> sourceSegments, float cutStart, float cutEnd)
    {
        List<FloatSegment> result = [];
        foreach (FloatSegment segment in sourceSegments)
        {
            if (cutEnd <= segment.Start || cutStart >= segment.End)
            {
                result.Add(segment);
                continue;
            }

            if (cutStart > segment.Start)
            {
                result.Add(new FloatSegment(segment.Start, MathF.Min(cutStart, segment.End)));
            }

            if (cutEnd < segment.End)
            {
                result.Add(new FloatSegment(MathF.Max(cutEnd, segment.Start), segment.End));
            }
        }

        return result;
    }

    private static void DrawStroke(NMapDrawings drawings, CanvasItem drawingsCanvas, Vector2 globalStart, Vector2 globalEnd)
    {
        Transform2D inverseTransform = drawingsCanvas.GetGlobalTransformWithCanvas().AffineInverse();
        Vector2 localStart = inverseTransform * globalStart;
        Vector2 localEnd = inverseTransform * globalEnd;
        if (localStart.DistanceSquaredTo(localEnd) < 16f)
        {
            return;
        }

        drawings.BeginLineLocal(localStart, DrawingMode.Drawing);
        drawings.UpdateCurrentLinePositionLocal(localEnd);
        drawings.StopLineLocal();
    }

    private static Texture2D? ResolveLegendIcon(NMapScreen screen)
    {
        if (GetFieldValue<Control>(screen, "_legendItems") is { } legendItems)
        {
            foreach (Node child in legendItems.GetChildren())
            {
                Texture2D? icon = TryResolveTexture(child);
                if (icon != null)
                {
                    return icon;
                }
            }
        }

        if (TryResolveTexture(GetFieldValue<object>(screen, "_mapDrawingButton")) is { } drawButtonIcon)
        {
            return drawButtonIcon;
        }

        return null;
    }

    private static Texture2D? TryResolveTexture(object? source)
    {
        if (source == null)
        {
            return null;
        }

        if (source is TextureRect textureRect && textureRect.Texture != null)
        {
            return textureRect.Texture;
        }

        if (GetFieldValue<TextureRect>(source, "_icon") is { Texture: not null } icon)
        {
            return icon.Texture;
        }

        if (source is Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                Texture2D? childTexture = TryResolveTexture(child);
                if (childTexture != null)
                {
                    return childTexture;
                }
            }
        }

        return null;
    }

    private static void AddExcludedNode(HashSet<Node> excludedNodes, Node? node)
    {
        if (node != null)
        {
            excludedNodes.Add(node);
        }
    }

    private static bool TryGetNodeCenter(Node? node, out Vector2 center)
    {
        switch (node)
        {
            case Control control:
                Rect2 rect = control.GetGlobalRect();
                center = rect.Position + (rect.Size * 0.5f);
                return true;
            case Node2D node2D:
                center = node2D.GlobalPosition;
                return true;
            case CanvasItem canvasItem:
                center = canvasItem.GetGlobalTransformWithCanvas().Origin;
                return true;
            default:
                center = default;
                return false;
        }
    }

    private static float EstimateNodeRadius(Node? node)
    {
        if (node is Control control)
        {
            return MathF.Max(MathF.Max(control.Size.X, control.Size.Y) * 0.6f, 36f);
        }

        if (node is Node2D)
        {
            return 56f;
        }

        return 48f;
    }

    private static T? GetFieldValue<T>(object source, string fieldName) where T : class
    {
        FieldInfo? field = AccessTools.Field(source.GetType(), fieldName);
        return field?.GetValue(source) as T;
    }

    private static void InvokeMethod(object source, string methodName)
    {
        MethodInfo? method = AccessTools.Method(source.GetType(), methodName);
        method?.Invoke(source, []);
    }
}
