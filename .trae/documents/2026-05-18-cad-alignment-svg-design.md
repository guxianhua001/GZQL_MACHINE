# CAD Alignment Principle SVG — Design Spec

> **Date**: 2026-05-18
> **Status**: Approved
> **Approach**: Option A — Hand-crafted SVG + XAML Synchronous Update
> **Language**: Bilingual (zh/en dual files + ResourceDictionary switching)

---

## 1. Problem Statement

Current `CadAlignmentPrincipleWindow.xaml` has 3 visual issues (confirmed by screenshot):
1. **P1-P2 is NOT horizontal** — tilted ~6.5° (Y diff = 13px between endpoints)
2. **Core formula covered by step bar** — bottom text overlaps with step indicator bar
3. **Text labels crowded** — middle transition area text crosses trajectory lines

Additionally:
- Need **independent SVG file** for external use (docs/web/PPT)
- Need **English + Chinese bilingual support**

---

## 2. Geometry Specification

### 2.1 Circle Parameters

| Parameter | Value |
|-----------|-------|
| Center O (left panel) | `(230, 305)` |
| Radius R | `138` |
| Arc shape | C-shape, opening upward, span ~240° |
| Arc command | `M 92 293 A 138 138 0 1 1 368 293` |
| Center O' (right panel) | `(740, 305)` (= O + ΔX 510, same Y) |

### 2.2 Point Pairs — All Endpoints on Circle

**Verification**: All points satisfy `(x - 230)² + (y - 305)² = 138² = 19044`

#### P1-P2: Red Baseline (HORIZONTAL ✅)

| Point | X | Y | Color | Note |
|-------|---|---|-------|------|
| P1 | 288.3 | 430.1 | #E53935 | Right endpoint |
| P2 | 171.7 | 430.1 | #E53935 | Left endpoint |

- Y coordinates identical → **perfectly horizontal**
- Chord length: 116.6px
- Angle from center: ±65° from vertical-down

#### P3-P4: Green Chord (right side)

| Point | X | Y | Color | Note |
|-------|---|---|-------|------|
| P3 | 355.1 | 246.7 | #43A047 | Lower-right on arc |
| P4 | 309.2 | 192.0 | #43A047 | Upper-right on arc |

#### P5-P6: Purple Chord (left side)

| Point | X | Y | Color | Note |
|-------|---|---|-------|------|
| P5 | 150.8 | 192.0 | #7B1FA2 | Upper-left on arc |
| P6 | 104.9 | 246.7 | #7B1FA2 | Lower-left on arc |

#### Pa-Pb: Orange Chord (far left)

| Point | X | Y | Color | Note |
|-------|---|---|-------|------|
| Pa | 92.3 | 293.0 | #F57C00 | Far-left on arc |
| Pb | 117.0 | 225.8 | #F57C00 | Mid-left on arc |

### 2.3 Right Panel (After Rotation) Coordinates

All right-panel points = left-panel point + **ΔX = 510**, except:

| Line | Y-coordinate (after rotation) | Status |
|------|------------------------------|--------|
| P1'-P2' | Same as original (430.1) | Baseline unchanged |
| P3'-P4' | **335** (horizontalized) | ✓ Leveled |
| P5'-P6' | **265** (horizontalized) | ✓ Leveled |
| Pa'-Pb' | **200** (horizontalized) | ✓ Leveled |

Rotated chords are horizontal, starting at X ≈ 608 (left margin in right panel).

---

## 3. Layout Structure

### 3.1 Canvas/SVG Dimensions

```
Total: 960 × 640 pixels
├── Title:          Y=10,   H=24
├── Left Panel:     X=24, Y=42,  W=420, H=490
├── Center Arrow:   X=456~526, Y=285
├── Right Panel:    X=534, Y=42, W=390, H=490
├── Step Bar:       X=176, Y=555, W=608, H=48
└── Formula Text:   X=center, Y=592, H=20
```

### 3.2 Anti-Overlap Design

| Element | Y Position | Height | Gap to Next |
|---------|-----------|--------|-------------|
| Panel bottom | 42+490 = 532 | — | 23px |
| Step Bar top | 555 | 48 | — |
| Step Bar bottom | 555+48 = 603 | — | **—** |
| Formula top | 592 | 20 | **Step bar ends ABOVE formula** ← wait this is wrong |

**Correction**: Formula must be BELOW step bar:
- Step Bar: Y=550, H=48 → bottom at 598
- Formula: Y=608, H=20 → bottom at 628
- Canvas height: 640 → 32px margin below formula ✅

---

## 4. Text Resources (Bilingual)

### 4.1 English Strings (for SVG_en + default XAML)

| Key | English Text |
|-----|-------------|
| Title | `CAD Alignment — 5-Step Standard Process` |
| PanelLeftTitle | `Before Rotation — CAD Original Pose` |
| PanelRightTitle | `After Rotation — Mechanism Coordinates Aligned` |
| LabelP1 | `P1` |
| LabelP2 | `P2` |
| LabelP3 | `P3` |
| LabelP4 | `P4` |
| LabelP5 | `P5` |
| LabelP6 | `P6` |
| LabelPa | `Pa` |
| LabelPb | `Pb` |
| LabelP1p | `P1'` |
| LabelP2p | `P2'` |
| LabelP3p | `P3'` |
| LabelP4p | `P4'` |
| LabelP5p | `P5'` |
| LabelP6p | `P6'` |
| LabelPap | `Pa'` |
| LabelPbp | `Pb'` |
| BaselineNote | `Baseline Chord 0°` |
| BaselineNoteRight | `Baseline (fixed)` |
| LeveledNote | `✓ Leveled to baseline` |
| CenterLabel | `O` |
| CenterCoord | `(Mox,Moy)` |
| CenterDesc | `Rz Center = Arc Center` |
| CenterDescRight | `Center unchanged` |
| ThetaLabel | `θ` |
| Step1Text | `① Find Center` |
| Step2Text | `② Calc Offset` |
| Step3Text | `③ CAD Angle` |
| Step4Text | `④ Translate then Rotate` |
| Step5Text | `⑤ Gripper Position` |
| TransitionSubtext | `Align all to baseline direction` |
| StepBar1 | `① Center` |
| StepBar2 | `② Offset` |
| StepBar3 | `③ Angle` |
| StepBar4 | `④ Transform` |
| StepBar5 | `⑤ Gripper` |
| CoreFormula | `Core: Xm=Cx+ΔX → dx=Xm-Mox → X_new=dx·cosθ-dy·sinθ+Mox  |  GX=X_new+OffX` |
| ExportBtn | `Export DXF to Desktop` |

### 4.2 Chinese Strings (for SVG_zh + XAML zh-CN ResourceDictionary)

| Key | Chinese Text |
|-----|-------------|
| Title | `CAD 对位 — 5 步标准流程原理` |
| PanelLeftTitle | `旋转前 — CAD 原始姿态` |
| PanelRightTitle | `旋转后 — 机械坐标对齐` |
| BaselineNote | `基准线段 0°` |
| BaselineNoteRight | `基准（不变）` |
| LeveledNote | `✓ 已摆正至水平` |
| CenterDesc | `Rz 回转中心 = 圆心` |
| CenterDescRight | `回转中心不动` |
| Step1Text | `① 求回转中心` |
| Step2Text | `② 算全局偏移` |
| Step3Text | `③ CAD 角度` |
| Step4Text | `④ 先平移后旋转` |
| Step5Text | `⑤ 夹爪定位` |
| TransitionSubtext | `统一摆正至基准方向` |
| StepBar1 | `① 回转中心` |
| StepBar2 | `② 全局偏移` |
| StepBar3 | `③ 旋转角度` |
| StepBar4 | `④ 坐标变换` |
| StepBar5 | `⑤ 夹爪定位` |
| CoreFormula | `核心公式: Xm=Cx+ΔX → dx=Xm-Mox → X_new=dx·cosθ-dy·sinθ+Mox  |  Gripper_X = X_new + OffX` |
| ExportBtn | `导出DXF到桌面` |

Point labels (P1/P2/Pa/Pb etc.) and math symbols (θ/O) remain **unchanged** across languages.

---

## 5. Output Files

| File | Path | Description |
|------|------|-------------|
| SVG English | `Resources/Svg/CadAlignmentPrinciple_en.svg` | Standalone English SVG |
| SVG Chinese | `Resources/Svg/CadAlignmentPrinciple_zh.svg` | Standalone Chinese SVG |
| XAML Window | `Module/Controls/Assembly/CadAlignmentPrincipleWindow.xaml` | Updated Canvas with DynamicResource |
| XAML Code-behind | `Module/Controls/Assembly/CadAlignmentPrincipleWindow.xaml.cs` | DXF export coords synced |
| ResourceDict EN | `Resources/Strings/CadAlignmentPrinciple.en.xaml` | English string resources |
| ResourceDict ZH | `Resources/Strings/CadAlignmentPrinciple.zh.xaml` | Chinese string resources |

---

## 6. Implementation Steps

1. Create SVG English file with precise geometry (Section 2)
2. Create SVG Chinese file (copy EN, replace text per Section 4.2)
3. Rewrite XAML Canvas geometry to match SVG (same coordinates)
4. Replace all hardcoded Text strings with `{DynamicResource Key}`
5. Create English/Chinese ResourceDictionary XAML files
6. Sync DXF export coordinate data in code-behind
7. Verify build (close VS first to avoid lock errors)
