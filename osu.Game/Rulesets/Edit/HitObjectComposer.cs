// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit.Screens.Compose;
using osu.Game.Screens.Edit.Screens.Compose.Layers;
using osu.Game.Screens.Edit.Screens.Compose.RadioButtons;

namespace osu.Game.Rulesets.Edit
{
    public abstract class HitObjectComposer : CompositeDrawable
    {
        private readonly Ruleset ruleset;

        public IEnumerable<DrawableHitObject> HitObjects => rulesetContainer.Playfield.AllHitObjects;

        protected IRulesetConfigManager Config { get; private set; }

        private readonly List<Container> layerContainers = new List<Container>();
        private readonly IBindable<WorkingBeatmap> beatmap = new Bindable<WorkingBeatmap>();

        [Resolved]
        private IPlacementHandler placementHandler { get; set; }

        private HitObjectMaskLayer maskLayer;
        private EditRulesetContainer rulesetContainer;

        private readonly Bindable<HitObjectCompositionTool> compositionTool = new Bindable<HitObjectCompositionTool>();

        protected HitObjectComposer(Ruleset ruleset)
        {
            this.ruleset = ruleset;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(IBindableBeatmap beatmap, IFrameBasedClock framedClock)
        {
            this.beatmap.BindTo(beatmap);

            try
            {
                rulesetContainer = CreateRulesetContainer(ruleset, beatmap.Value);
                rulesetContainer.Clock = framedClock;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Could not load beatmap sucessfully!");
                return;
            }

            var layerBelowRuleset = new BorderLayer
            {
                RelativeSizeAxes = Axes.Both,
                Child = CreateLayerContainer()
            };

            var layerAboveRuleset = CreateLayerContainer();
            layerAboveRuleset.Children = new Drawable[]
            {
                maskLayer = new HitObjectMaskLayer(),
                new PlacementContainer(compositionTool),
            };

            layerContainers.Add(layerBelowRuleset);
            layerContainers.Add(layerAboveRuleset);

            RadioButtonCollection toolboxCollection;
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Content = new[]
                {
                    new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Name = "Sidebar",
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Right = 10 },
                            Children = new Drawable[]
                            {
                                new ToolboxGroup { Child = toolboxCollection = new RadioButtonCollection { RelativeSizeAxes = Axes.X } }
                            }
                        },
                        new Container
                        {
                            Name = "Content",
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                layerBelowRuleset,
                                rulesetContainer,
                                layerAboveRuleset
                            }
                        }
                    },
                },
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 200),
                }
            };

            toolboxCollection.Items =
                CompositionTools.Select(t => new RadioButton(t.Name, () => compositionTool.Value = t))
                .Prepend(new RadioButton("Select", () => compositionTool.Value = null))
                .ToList();

            toolboxCollection.Items[0].Select();

            // Todo: no
            placementHandler.PlacementFinished += h => maskLayer.AddMask(rulesetContainer.AddHitObject(h));
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            dependencies.CacheAs(this);
            Config = dependencies.Get<RulesetConfigCache>().GetConfigFor(ruleset);

            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rulesetContainer.Playfield.DisplayJudgements.Value = false;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            layerContainers.ForEach(l =>
            {
                l.Anchor = rulesetContainer.Playfield.Anchor;
                l.Origin = rulesetContainer.Playfield.Origin;
                l.Position = rulesetContainer.Playfield.Position;
                l.Size = rulesetContainer.Playfield.Size;
            });
        }

        protected abstract EditRulesetContainer CreateRulesetContainer(Ruleset ruleset, WorkingBeatmap beatmap);

        protected abstract IReadOnlyList<HitObjectCompositionTool> CompositionTools { get; }

        /// <summary>
        /// Creates a <see cref="SelectionMask"/> for a specific <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <param name="hitObject">The <see cref="DrawableHitObject"/> to create the overlay for.</param>
        public virtual SelectionMask CreateMaskFor(DrawableHitObject hitObject) => null;

        /// <summary>
        /// Creates a <see cref="MaskSelection"/> which outlines <see cref="DrawableHitObject"/>s
        /// and handles hitobject pattern adjustments.
        /// </summary>
        public virtual MaskSelection CreateMaskSelection() => new MaskSelection();

        /// <summary>
        /// Creates a <see cref="ScalableContainer"/> which provides a layer above or below the <see cref="Playfield"/>.
        /// </summary>
        protected virtual Container CreateLayerContainer() => new Container { RelativeSizeAxes = Axes.Both };
    }
}
