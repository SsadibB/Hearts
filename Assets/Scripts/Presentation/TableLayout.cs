using System.Text;
using UnityEngine;

namespace Hearts.Presentation
{
    public struct TableMetrics
    {
        public float canvasWidth;
        public float canvasHeight;
        public float insetLeft;
        public float insetRight;
        public float insetBottom;
        public float insetTop;
        public Vector2 cardSize;
        public Vector2 localCardSize;
        public float selectionLift;
        public Vector2 bottomHudCenter;
        public Vector2 bottomHudSize;
        public Vector2 topHudCenter;
        public Vector2 topHudSize;
        public Vector2 leftHudCenter;
        public Vector2 leftHudSize;
        public Vector2 rightHudCenter;
        public Vector2 rightHudSize;
        public float handCenterY;
        public float handLeft;
        public float handRight;
        public float topFanY;
        public float topFanLeft;
        public float topFanRight;
        public float sideBaseY;
        public float sideTravel;
        public float leftCardX;
        public float rightCardX;
        public float leftRotation;
        public float rightRotation;
        public Vector2 trickCenter;
        public float trickOffsetX;
        public float trickOffsetY;
        public Vector2 statusCenter;
        public Vector2 statusSize;
        public Vector2 leaveCenter;
        public Vector2 leaveSize;
        public Vector2 passCenter;
        public Vector2 passSize;
        public Vector2 deckCenter;
    }

    public struct MenuMetrics
    {
        public Vector2 titleCenter;
        public Vector2 titleSize;
        public int titleFont;
        public Vector2 avatarCenter;
        public Vector2 avatarSize;
        public Vector2 nameCenter;
        public Vector2 nameSize;
        public Vector2 singleCenter;
        public Vector2 singleSize;
        public Vector2 multiCenter;
        public Vector2 multiSize;
        public Vector2 settingsCenter;
        public Vector2 settingsSize;
        public Vector2 statusCenter;
        public Vector2 statusSize;
    }

    public static class TableLayout
    {
        public const float LeftRotation = -90f;
        public const float RightRotation = 90f;

        public static TableMetrics Compute(float width, float height)
        {
            return Compute(width, height, 0f, 0f, 0f, 0f);
        }

        public static TableMetrics Compute(float width, float height, float insetLeft, float insetRight, float insetBottom, float insetTop)
        {
            float canvasW = Mathf.Max(160f, width);
            float canvasH = Mathf.Max(160f, height);
            float left = Mathf.Clamp(insetLeft, 0f, canvasW * 0.3f);
            float right = Mathf.Clamp(insetRight, 0f, canvasW * 0.3f);
            float bottom = Mathf.Clamp(insetBottom, 0f, canvasH * 0.3f);
            float top = Mathf.Clamp(insetTop, 0f, canvasH * 0.3f);
            float cardW = Mathf.Min(112f, (canvasW - left - right) / 8.4f);
            cardW = Mathf.Max(30f, cardW);
            float hudScale = 1f;

            TableMetrics best = default;
            TableMetrics fitted = default;
            bool haveBest = false;
            bool haveFitted = false;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                TableMetrics candidate = Build(canvasW, canvasH, left, right, bottom, top, cardW, hudScale);
                best = candidate;
                haveBest = true;
                if (Fits(candidate))
                {
                    fitted = candidate;
                    haveFitted = true;
                    if (CrossReady(candidate))
                        return candidate;
                }
                if (cardW > 32f)
                    cardW = Mathf.Max(30f, cardW * 0.88f);
                else
                    hudScale = Mathf.Max(0.45f, hudScale * 0.88f);
            }

            if (haveFitted)
                return fitted;
            return haveBest ? best : Build(canvasW, canvasH, left, right, bottom, top, 36f, 0.5f);
        }

        static bool CrossReady(TableMetrics metrics)
        {
            float wantY = Mathf.Min(16f, metrics.cardSize.y * 0.14f);
            float wantX = Mathf.Min(16f, metrics.cardSize.x * 0.18f);
            if (metrics.canvasWidth >= metrics.canvasHeight * 1.05f)
                return metrics.trickOffsetY >= wantY - 0.5f || metrics.cardSize.x <= 36f;
            if (metrics.canvasHeight >= metrics.canvasWidth * 1.15f)
                return metrics.trickOffsetX >= wantX - 0.5f || metrics.cardSize.x <= 34f;
            return metrics.trickOffsetX >= wantX - 0.5f && metrics.trickOffsetY >= wantY - 0.5f;
        }

        public static MenuMetrics ComputeMenu(float width, float height)
        {
            return ComputeMenu(width, height, 0f, 0f, 0f, 0f);
        }

        public static MenuMetrics ComputeMenu(float width, float height, float insetLeft, float insetRight, float insetBottom, float insetTop)
        {
            float canvasW = Mathf.Max(160f, width);
            float canvasH = Mathf.Max(160f, height);
            float x0 = Mathf.Clamp(insetLeft, 0f, canvasW * 0.3f);
            float x1 = canvasW - Mathf.Clamp(insetRight, 0f, canvasW * 0.3f);
            float y0 = Mathf.Clamp(insetBottom, 0f, canvasH * 0.3f);
            float y1 = canvasH - Mathf.Clamp(insetTop, 0f, canvasH * 0.3f);
            float usableW = Mathf.Max(80f, x1 - x0);
            float usableH = Mathf.Max(80f, y1 - y0);
            float pad = Mathf.Clamp(Mathf.Min(usableW, usableH) * 0.02f, 6f, 18f);
            float innerW = Mathf.Max(48f, usableW - pad * 2f);
            float statusH = Mathf.Clamp(usableH * 0.06f, 24f, 40f);
            float titleH = Mathf.Clamp(usableH * 0.09f, 32f, 72f);
            float avatar = Mathf.Clamp(usableH * 0.12f, 44f, 96f);
            float fieldH = Mathf.Clamp(usableH * 0.07f, 32f, 52f);
            float btnH = Mathf.Clamp(usableH * 0.08f, 32f, 64f);
            float gap = Mathf.Clamp(usableH * 0.016f, 4f, 14f);
            float block = titleH + avatar + fieldH + btnH * 3f + gap * 5f;
            float available = usableH - pad * 2f - statusH - gap;
            if (block > available && block > 1f)
            {
                float scale = Mathf.Max(0.35f, available / block);
                titleH *= scale;
                avatar *= scale;
                fieldH *= scale;
                btnH *= scale;
                gap *= scale;
            }

            avatar = Mathf.Min(avatar, innerW);
            float buttonW = Mathf.Min(420f, innerW);
            float cx = x0 + usableW * 0.5f;
            var menu = new MenuMetrics();
            float y = y1 - pad - titleH * 0.5f;
            menu.titleCenter = new Vector2(cx, y);
            menu.titleSize = new Vector2(Mathf.Min(720f, innerW), titleH);
            menu.titleFont = titleH >= 56f ? 64 : titleH >= 40f ? 40 : 28;

            y -= titleH * 0.5f + gap + avatar * 0.5f;
            menu.avatarCenter = new Vector2(cx, y);
            menu.avatarSize = new Vector2(avatar, avatar);

            y -= avatar * 0.5f + gap + fieldH * 0.5f;
            menu.nameCenter = new Vector2(cx, y);
            menu.nameSize = new Vector2(buttonW, fieldH);

            y -= fieldH * 0.5f + gap + btnH * 0.5f;
            menu.singleCenter = new Vector2(cx, y);
            menu.singleSize = new Vector2(buttonW, btnH);

            y -= btnH + gap;
            menu.multiCenter = new Vector2(cx, y);
            menu.multiSize = new Vector2(buttonW, btnH);

            y -= btnH + gap;
            menu.settingsCenter = new Vector2(cx, y);
            menu.settingsSize = new Vector2(buttonW, btnH);

            menu.statusCenter = new Vector2(cx, y0 + pad + statusH * 0.5f);
            menu.statusSize = new Vector2(innerW, statusH);
            return menu;
        }

        public static float PopupScale(float canvasW, float canvasH, float preferredW, float preferredH)
        {
            float sx = (Mathf.Max(80f, canvasW) - 24f) / Mathf.Max(1f, preferredW);
            float sy = (Mathf.Max(80f, canvasH) - 24f) / Mathf.Max(1f, preferredH);
            return Mathf.Clamp(Mathf.Min(sx, sy), 0.35f, 1f);
        }

        public static float Step(float left, float right, int count, float preferred)
        {
            if (count <= 1)
                return 0f;
            float maxStep = Mathf.Max(0f, right - left) / (count - 1);
            return Mathf.Min(Mathf.Max(0f, preferred), maxStep);
        }

        public static float FanStart(float left, float right, float span)
        {
            float start = (left + right) * 0.5f - span * 0.5f;
            float maxStart = right - span;
            if (maxStart < left)
                return (left + right - span) * 0.5f;
            return Mathf.Clamp(start, left, maxStart);
        }

        public static Vector2 ClampCard(TableMetrics metrics, Vector2 center, bool rotated)
        {
            float halfW = (rotated ? metrics.cardSize.y : metrics.cardSize.x) * 0.5f;
            float halfH = (rotated ? metrics.cardSize.x : metrics.cardSize.y) * 0.5f;
            center.x = ClampAxis(center.x, metrics.insetLeft + halfW, metrics.canvasWidth - metrics.insetRight - halfW);
            center.y = ClampAxis(center.y, metrics.insetBottom + halfH, metrics.canvasHeight - metrics.insetTop - halfH);
            return center;
        }

        public static string Report(float width, float height)
        {
            var builder = new StringBuilder();
            AppendProblems(builder, Compute(width, height), ComputeMenu(width, height));
            return builder.Length == 0 ? "ok" : builder.ToString();
        }

        static TableMetrics Build(float canvasW, float canvasH, float insetL, float insetR, float insetB, float insetT, float cardW, float hudScale)
        {
            float x0 = insetL;
            float x1 = canvasW - insetR;
            float y0 = insetB;
            float y1 = canvasH - insetT;
            float usableW = Mathf.Max(40f, x1 - x0);
            float usableH = Mathf.Max(40f, y1 - y0);
            float pad = Mathf.Clamp(Mathf.Min(usableW, usableH) * 0.018f, 4f, 18f);
            float cardH = cardW * 1.4f;
            float localW = cardW * 1.2f;
            float localH = localW * 1.4f;
            float hudH = Mathf.Clamp(usableH * 0.105f, 44f, 100f) * hudScale;
            float bottomHudH;
            float gap = Mathf.Clamp(Mathf.Min(usableW, usableH) * 0.012f, 3f, 12f);
            float leaveW = Mathf.Clamp(usableW * 0.13f, 84f, 136f);
            float leaveH = Mathf.Min(Mathf.Clamp(hudH * 0.55f, 30f, 44f), cardH);

            var metrics = new TableMetrics();
            metrics.canvasWidth = canvasW;
            metrics.canvasHeight = canvasH;
            metrics.insetLeft = insetL;
            metrics.insetRight = insetR;
            metrics.insetBottom = insetB;
            metrics.insetTop = insetT;
            metrics.cardSize = new Vector2(cardW, cardH);
            metrics.localCardSize = new Vector2(localW, localH);
            metrics.leftRotation = LeftRotation;
            metrics.rightRotation = RightRotation;

            float localAvatar = Mathf.Clamp(usableH * 0.1f, 56f, 104f) * hudScale;
            bottomHudH = localAvatar + 12f;
            float bottomHudW = Mathf.Min(460f, Mathf.Max(localAvatar + 168f, usableW * 0.34f));
            metrics.bottomHudSize = new Vector2(bottomHudW, bottomHudH);
            metrics.bottomHudCenter = new Vector2(x0 + usableW * 0.5f, y0 + pad + bottomHudH * 0.5f);
            float liftWanted = Mathf.Clamp(localH * 0.18f, 16f, 36f);
            metrics.selectionLift = liftWanted;
            metrics.handCenterY = metrics.bottomHudCenter.y - bottomHudH * 0.5f + 90f;

            metrics.leaveSize = new Vector2(leaveW, leaveH);
            metrics.leaveCenter = new Vector2(x1 - pad - leaveW * 0.5f, y1 - pad - leaveH * 0.5f);
            float topAvatar = Mathf.Clamp(usableH * 0.09f, 48f, 96f) * hudScale;
            float topHudH = topAvatar + 16f;
            float topCenterX = x0 + usableW * 0.5f;
            float topHudRight = metrics.leaveCenter.x - leaveW * 0.5f - gap;
            float topHudRoom = Mathf.Max(96f, (topHudRight - topCenterX) * 2f);
            float topHudW = Mathf.Min(420f, Mathf.Max(topAvatar + 150f, Mathf.Min(topHudRoom, usableW * 0.4f)));
            metrics.topHudSize = new Vector2(topHudW, topHudH);
            metrics.topHudCenter = new Vector2(topCenterX, y1 - pad - topHudH * 0.5f);

            float sideAvatar = Mathf.Clamp(usableH * 0.085f, 44f, 92f) * hudScale;
            float sideName = Mathf.Clamp(usableH * 0.032f, 18f, 30f) * hudScale;
            float sideMeta = Mathf.Clamp(usableH * 0.044f, 24f, 40f) * hudScale;
            float sideHudW = Mathf.Min(Mathf.Max(sideAvatar + 18f, 76f), usableW * 0.18f);
            float sideHudH = sideAvatar + sideName + sideMeta + gap * 2f;
            float sideBandTop = metrics.topHudCenter.y - topHudH * 0.5f - gap;
            float sideBandBottom = metrics.bottomHudCenter.y + bottomHudH * 0.5f + gap;
            float sideHudY = (y0 + y1) * 0.5f;
            float sideMinY = sideBandBottom + sideHudH * 0.5f;
            float sideMaxY = sideBandTop - sideHudH * 0.5f;
            if (sideMaxY >= sideMinY)
                sideHudY = Mathf.Clamp(sideHudY, sideMinY, sideMaxY);
            metrics.leftHudSize = new Vector2(sideHudW, sideHudH);
            metrics.rightHudSize = new Vector2(sideHudW, sideHudH);
            metrics.leftHudCenter = new Vector2(x0 + pad + sideHudW * 0.5f, sideHudY);
            metrics.rightHudCenter = new Vector2(x1 - pad - sideHudW * 0.5f, sideHudY);

            float rotatedHalfH = cardW * 0.5f;
            float rotatedHalfW = cardH * 0.5f;
            float minCardX = x0 + pad + rotatedHalfW;
            float maxCardX = x1 - pad - rotatedHalfW;
            metrics.leftCardX = Mathf.Clamp(metrics.leftHudCenter.x + sideHudW * 0.5f + gap + rotatedHalfW, minCardX, maxCardX);
            metrics.rightCardX = Mathf.Clamp(metrics.rightHudCenter.x - sideHudW * 0.5f - gap - rotatedHalfW, minCardX, maxCardX);
            metrics.handLeft = x0 + pad + localW * 0.5f;
            metrics.handRight = x1 - pad - localW * 0.5f;

            float topArc = cardH * 0.12f;
            metrics.topFanY = metrics.topHudCenter.y - topHudH * 0.5f - gap - cardH * 0.5f;
            metrics.topFanLeft = x0 + pad + cardW * 0.5f;
            float fanRightLimit = metrics.leaveCenter.x - leaveW * 0.5f - gap - cardW * 0.5f;
            metrics.topFanRight = Mathf.Min(x1 - pad - cardW * 0.5f, fanRightLimit);
            if (metrics.topFanRight < metrics.topFanLeft)
                metrics.topFanRight = metrics.topFanLeft;

            float fanBottom = metrics.topFanY - topArc - cardH * 0.5f;
            float sideMidY = (y0 + y1) * 0.5f;
            float sideLowLimit = metrics.handCenterY + localH * 0.5f + metrics.selectionLift + gap + rotatedHalfH;
            float sideHighLimit = fanBottom - gap - rotatedHalfH;
            float roomDown = Mathf.Max(0f, sideMidY - sideLowLimit);
            float roomUp = Mathf.Max(0f, sideHighLimit - sideMidY);
            float wantedHalf = cardW * 0.52f * 6f;
            float half = Mathf.Min(wantedHalf, roomDown, roomUp);
            metrics.sideBaseY = sideMidY - half;
            metrics.sideTravel = half * 2f;

            float bow = cardW * 0.16f;
            float holeLeft = metrics.leftCardX + bow + rotatedHalfW + gap;
            float holeRight = metrics.rightCardX - bow - rotatedHalfW - gap;
            float holeBottom = metrics.handCenterY + localH * 0.5f + gap;
            float statusH = Mathf.Clamp(usableH * 0.034f, 16f, 28f);
            float statusW = Mathf.Min(520f, Mathf.Max(80f, usableW * 0.46f));
            metrics.statusSize = new Vector2(statusW, statusH);
            metrics.statusCenter = new Vector2(
                x0 + usableW * 0.5f,
                Mathf.Clamp(fanBottom - gap - statusH * 0.5f, holeBottom + statusH * 0.5f, y1 - pad - statusH * 0.5f));
            float holeTop = metrics.statusCenter.y - statusH * 0.5f - gap;
            float holeMidX = holeRight > holeLeft ? (holeLeft + holeRight) * 0.5f : x0 + usableW * 0.5f;
            float holeMidY = holeTop > holeBottom ? (holeBottom + holeTop) * 0.5f : (metrics.handCenterY + metrics.topHudCenter.y) * 0.5f;
            metrics.trickCenter = new Vector2(holeMidX, holeMidY);
            metrics.deckCenter = metrics.trickCenter;
            float holeW = Mathf.Max(0f, holeRight - holeLeft);
            float holeH = Mathf.Max(0f, holeTop - holeBottom);
            float maxCrossX = Mathf.Max(0f, (holeW - cardW) * 0.5f);
            float maxCrossY = Mathf.Max(0f, (holeH - cardH) * 0.5f);
            metrics.trickOffsetX = Mathf.Min(cardW * 0.38f, maxCrossX);
            metrics.trickOffsetY = Mathf.Min(cardH * 0.36f, maxCrossY);

            float pass = Mathf.Clamp(Mathf.Min(cardH, Mathf.Max(holeW, cardW * 0.8f)), 36f, 112f);
            if (float.IsNaN(pass) || pass < 36f)
                pass = 36f;
            metrics.passSize = new Vector2(pass, pass);
            float handTop = metrics.handCenterY + localH * 0.5f + metrics.selectionLift;
            float passY = Mathf.Max(metrics.trickCenter.y, handTop + gap + pass * 0.5f);
            metrics.passCenter = new Vector2(metrics.trickCenter.x, passY);
            metrics.passCenter.x = ClampAxis(metrics.passCenter.x, x0 + pad + pass * 0.5f, x1 - pad - pass * 0.5f);
            metrics.passCenter.y = ClampAxis(metrics.passCenter.y, y0 + pad + pass * 0.5f, y1 - pad - pass * 0.5f);
            return metrics;
        }

        static bool Fits(TableMetrics metrics)
        {
            if (!RectInside(metrics, metrics.bottomHudCenter, metrics.bottomHudSize))
                return false;
            if (!RectInside(metrics, metrics.topHudCenter, metrics.topHudSize))
                return false;
            if (!RectInside(metrics, metrics.leftHudCenter, metrics.leftHudSize))
                return false;
            if (!RectInside(metrics, metrics.rightHudCenter, metrics.rightHudSize))
                return false;
            if (!RectInside(metrics, metrics.leaveCenter, metrics.leaveSize))
                return false;
            if (!RectInside(metrics, metrics.passCenter, metrics.passSize))
                return false;
            if (!RectInside(metrics, metrics.statusCenter, metrics.statusSize))
                return false;
            if (metrics.handRight < metrics.handLeft - 0.1f || metrics.topFanRight < metrics.topFanLeft - 0.1f)
                return false;
            if (Overlaps(metrics.leftHudCenter, metrics.leftHudSize, metrics.topHudCenter, metrics.topHudSize))
                return false;
            if (Overlaps(metrics.rightHudCenter, metrics.rightHudSize, metrics.topHudCenter, metrics.topHudSize))
                return false;
            if (Overlaps(metrics.leftHudCenter, metrics.leftHudSize, metrics.bottomHudCenter, metrics.bottomHudSize))
                return false;
            Vector2 rotatedSize = new Vector2(metrics.cardSize.y, metrics.cardSize.x);
            for (int side = 0; side < 13; side++)
            {
                if (Overlaps(RawSide(metrics, side, 13, true), rotatedSize, metrics.leftHudCenter, metrics.leftHudSize))
                    return false;
                if (Overlaps(RawSide(metrics, side, 13, false), rotatedSize, metrics.rightHudCenter, metrics.rightHudSize))
                    return false;
            }
            if (Overlaps(RawSide(metrics, 0, 13, true), rotatedSize, metrics.bottomHudCenter, metrics.bottomHudSize))
                return false;
            if (Overlaps(RawSide(metrics, 0, 13, false), rotatedSize, metrics.bottomHudCenter, metrics.bottomHudSize))
                return false;
            if (Overlaps(RawSide(metrics, 0, 13, true), rotatedSize, RawHand(metrics, 0, 13, false), metrics.localCardSize))
                return false;
            if (Overlaps(RawSide(metrics, 0, 13, false), rotatedSize, RawHand(metrics, 12, 13, false), metrics.localCardSize))
                return false;
            if (Overlaps(RawSide(metrics, 12, 13, true), rotatedSize, RawTop(metrics, 0, 13), metrics.cardSize))
                return false;
            if (Overlaps(RawSide(metrics, 12, 13, false), rotatedSize, RawTop(metrics, 12, 13), metrics.cardSize))
                return false;
            if (Overlaps(RawSide(metrics, 12, 13, false), rotatedSize, metrics.leaveCenter, metrics.leaveSize))
                return false;
            if (Overlaps(RawTop(metrics, 12, 13), metrics.cardSize, metrics.leaveCenter, metrics.leaveSize))
                return false;
            if (Overlaps(RawHand(metrics, 0, 13, true), metrics.localCardSize, metrics.passCenter, metrics.passSize))
                return false;
            if (Overlaps(RawTrick(metrics, 0), metrics.cardSize, metrics.bottomHudCenter, metrics.bottomHudSize))
                return false;
            if (Overlaps(RawTrick(metrics, 2), metrics.cardSize, RawTop(metrics, 6, 13), metrics.cardSize))
                return false;
            Vector2 upright = metrics.cardSize;
            if (Overlaps(RawTrick(metrics, 1), upright, RawSide(metrics, 6, 13, true), rotatedSize))
                return false;
            if (Overlaps(RawTrick(metrics, 3), upright, RawSide(metrics, 6, 13, false), rotatedSize))
                return false;

            for (int i = 0; i < 13; i++)
            {
                if (!RectInside(metrics, RawHand(metrics, i, 13, false), metrics.localCardSize))
                    return false;
                if (!RectInside(metrics, RawHand(metrics, i, 13, true), metrics.localCardSize))
                    return false;
                if (!StableCard(metrics, RawTop(metrics, i, 13), false))
                    return false;
                if (!StableCard(metrics, RawSide(metrics, i, 13, true), true))
                    return false;
                if (!StableCard(metrics, RawSide(metrics, i, 13, false), true))
                    return false;
            }

            for (int play = 0; play < 4; play++)
            {
                if (!StableCard(metrics, RawTrick(metrics, play), false))
                    return false;
            }

            return true;
        }

        public static Vector2 HandPoint(TableMetrics metrics, int order, int count, bool selected)
        {
            return ClampCard(metrics, RawHand(metrics, order, count, selected), false);
        }

        public static Vector2 TopPoint(TableMetrics metrics, int order, int count)
        {
            return ClampCard(metrics, RawTop(metrics, order, count), false);
        }

        public static Vector2 SidePoint(TableMetrics metrics, int stack, int count, bool left)
        {
            return ClampCard(metrics, RawSide(metrics, stack, count, left), true);
        }

        public static Vector2 TrickPoint(TableMetrics metrics, int playIndex)
        {
            return ClampCard(metrics, RawTrick(metrics, playIndex), false);
        }

        public static float TrickRotation(int screen)
        {
            switch (screen)
            {
                case 1: return -4f;
                case 2: return -2f;
                case 3: return 4f;
                default: return 3f;
            }
        }

        public static Vector2 PilePoint(TableMetrics metrics, int screen, int trickOrdinal, int cardInTrick)
        {
            return TrickPoint(metrics, cardInTrick);
        }

        static Vector2 RawHand(TableMetrics metrics, int order, int count, bool selected)
        {
            int safeCount = Mathf.Max(1, count);
            int safeOrder = Mathf.Clamp(order, 0, safeCount - 1);
            float step = Step(metrics.handLeft, metrics.handRight, safeCount, metrics.localCardSize.x * 0.5f);
            float start = FanStart(metrics.handLeft, metrics.handRight, step * (safeCount - 1));
            var point = new Vector2(start + step * safeOrder, metrics.handCenterY);
            if (selected)
                point.y += Mathf.Max(8f, metrics.selectionLift);
            return point;
        }

        static Vector2 RawTop(TableMetrics metrics, int order, int count)
        {
            int safeCount = Mathf.Max(1, count);
            int safeOrder = Mathf.Clamp(order, 0, safeCount - 1);
            float step = Step(metrics.topFanLeft, metrics.topFanRight, safeCount, metrics.cardSize.x * 0.48f);
            float start = FanStart(metrics.topFanLeft, metrics.topFanRight, step * (safeCount - 1));
            float x = start + step * safeOrder;
            float along = safeCount <= 1 ? 0f : (safeOrder / (float)(safeCount - 1)) * 2f - 1f;
            float arc = metrics.cardSize.y * 0.12f;
            return new Vector2(x, metrics.topFanY - arc * (1f - along * along));
        }

        static Vector2 RawSide(TableMetrics metrics, int stack, int count, bool left)
        {
            int safeCount = Mathf.Max(1, count);
            int safeStack = Mathf.Clamp(stack, 0, safeCount - 1);
            float step = Step(0f, metrics.sideTravel, safeCount, metrics.cardSize.x * 0.52f);
            float span = step * (safeCount - 1);
            float start = metrics.sideBaseY + Mathf.Max(0f, metrics.sideTravel - span) * 0.5f;
            float along = safeCount <= 1 ? 0f : (safeStack / (float)(safeCount - 1)) * 2f - 1f;
            float bow = metrics.cardSize.x * 0.16f * (1f - along * along);
            float x = left ? metrics.leftCardX + bow : metrics.rightCardX - bow;
            return new Vector2(x, start + step * safeStack);
        }

        static Vector2 RawTrick(TableMetrics metrics, int screen)
        {
            float x = metrics.trickOffsetX;
            float y = metrics.trickOffsetY;
            switch (screen)
            {
                case 1: return metrics.trickCenter + new Vector2(-x, y * 0.08f);
                case 2: return metrics.trickCenter + new Vector2(-x * 0.1f, y);
                case 3: return metrics.trickCenter + new Vector2(x, -y * 0.06f);
                default: return metrics.trickCenter + new Vector2(x * 0.12f, -y);
            }
        }

        static bool Overlaps(Vector2 aCenter, Vector2 aSize, Vector2 bCenter, Vector2 bSize)
        {
            return Mathf.Abs(aCenter.x - bCenter.x) < (aSize.x + bSize.x) * 0.5f - 0.5f
                   && Mathf.Abs(aCenter.y - bCenter.y) < (aSize.y + bSize.y) * 0.5f - 0.5f;
        }

        static bool StableCard(TableMetrics metrics, Vector2 point, bool rotated)
        {
            if (!CardInside(metrics, point, rotated))
                return false;
            Vector2 clamped = ClampCard(metrics, point, rotated);
            return (clamped - point).sqrMagnitude < 0.25f;
        }

        static void AppendProblems(StringBuilder builder, TableMetrics metrics, MenuMetrics menu)
        {
            CheckRect(builder, "bottomHud", metrics, metrics.bottomHudCenter, metrics.bottomHudSize);
            CheckRect(builder, "topHud", metrics, metrics.topHudCenter, metrics.topHudSize);
            CheckRect(builder, "leftHud", metrics, metrics.leftHudCenter, metrics.leftHudSize);
            CheckRect(builder, "rightHud", metrics, metrics.rightHudCenter, metrics.rightHudSize);
            CheckRect(builder, "leave", metrics, metrics.leaveCenter, metrics.leaveSize);
            CheckRect(builder, "pass", metrics, metrics.passCenter, metrics.passSize);
            CheckRect(builder, "status", metrics, metrics.statusCenter, metrics.statusSize);
            CheckRect(builder, "menuTitle", metrics, menu.titleCenter, menu.titleSize);
            CheckRect(builder, "menuAvatar", metrics, menu.avatarCenter, menu.avatarSize);
            CheckRect(builder, "menuName", metrics, menu.nameCenter, menu.nameSize);
            CheckRect(builder, "menuSingle", metrics, menu.singleCenter, menu.singleSize);
            CheckRect(builder, "menuMulti", metrics, menu.multiCenter, menu.multiSize);
            CheckRect(builder, "menuSettings", metrics, menu.settingsCenter, menu.settingsSize);
            CheckRect(builder, "menuStatus", metrics, menu.statusCenter, menu.statusSize);
            for (int i = 0; i < 13; i++)
            {
                CheckRect(builder, "hand" + i, metrics, HandPoint(metrics, i, 13, false), metrics.localCardSize);
                CheckRect(builder, "handSel" + i, metrics, HandPoint(metrics, i, 13, true), metrics.localCardSize);
                CheckCard(builder, "top" + i, metrics, TopPoint(metrics, i, 13), false);
                CheckCard(builder, "left" + i, metrics, SidePoint(metrics, i, 13, true), true);
                CheckCard(builder, "right" + i, metrics, SidePoint(metrics, i, 13, false), true);
            }
        }

        static void CheckRect(StringBuilder builder, string name, TableMetrics metrics, Vector2 center, Vector2 size)
        {
            if (!RectInside(metrics, center, size))
                builder.Append(name).Append(" off; ");
        }

        static void CheckCard(StringBuilder builder, string name, TableMetrics metrics, Vector2 center, bool rotated)
        {
            if (!CardInside(metrics, center, rotated))
                builder.Append(name).Append(" off; ");
        }

        static bool RectInside(TableMetrics metrics, Vector2 center, Vector2 size)
        {
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;
            return center.x - halfW >= metrics.insetLeft - 0.6f
                   && center.x + halfW <= metrics.canvasWidth - metrics.insetRight + 0.6f
                   && center.y - halfH >= metrics.insetBottom - 0.6f
                   && center.y + halfH <= metrics.canvasHeight - metrics.insetTop + 0.6f;
        }

        static bool CardInside(TableMetrics metrics, Vector2 center, bool rotated)
        {
            Vector2 size = rotated
                ? new Vector2(metrics.cardSize.y, metrics.cardSize.x)
                : metrics.cardSize;
            return RectInside(metrics, center, size);
        }

        static float ClampAxis(float value, float min, float max)
        {
            if (max < min)
                return (min + max) * 0.5f;
            return Mathf.Clamp(value, min, max);
        }
    }
}
