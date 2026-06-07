const assert = require("assert");
const {
  CombatDamageType,
  ReactionEffectType,
  createInitialState,
  createTagDefinition,
  addReactionRule,
  applyAction,
  advanceTime,
} = require("../reaction-engine");

const state = createInitialState();
state.target = {
  name: "Enemy Dummy",
  maxHealth: 200,
  currentHealth: 200,
};
state.tagDefinitions = [
  createTagDefinition("ovl", "超频", 1, 6, 3),
  createTagDefinition("meltdown", "核心熔毁", 2, 4, 1),
  createTagDefinition("nos", "噪声", 1, 5, 3),
];
state.rules = [
  addReactionRule({
    firstTag: "ovl",
    secondTag: "ovl",
    requiredFirstStack: 3,
    requiredSecondStack: 3,
    priority: 10,
    reactionEffects: [
      { type: ReactionEffectType.ApplyTag, tag: "meltdown", stackCount: 1 },
      { type: ReactionEffectType.DealDamage, damageAmount: 35, damageType: CombatDamageType.RuntimeDamage },
      { type: ReactionEffectType.RemoveTag, tag: "ovl" },
    ],
  }),
];

const reactionState = applyAction(
  state,
  { name: "Player", atkPower: 20 },
  {
    name: "Test Skill",
    actionType: "Skill",
    flatDamage: 10,
    atkPowerMultiplier: 1.5,
    damageType: CombatDamageType.StructuralDamage,
    tags: ["ovl:5"],
  }
);

assert.strictEqual(reactionState.target.currentHealth, 125);
assert.deepStrictEqual(reactionState.targetTags, [
  { id: "meltdown", displayName: "核心熔毁", level: 2, stacks: 1, duration: 4 },
]);
assert.strictEqual(reactionState.linkWindow.remaining, 4);
assert.strictEqual(reactionState.timeline[1].message, "Deal 40 StructuralDamage");
assert.strictEqual(reactionState.timeline[2].message, "Apply 超频 x3 (6s)");
assert.strictEqual(reactionState.timeline[3].message, "Reaction ovl + ovl");
assert.strictEqual(reactionState.timeline[4].message, "Apply 核心熔毁 (4s)");
assert.strictEqual(reactionState.timeline[5].message, "Deal 35 RuntimeDamage");
assert.strictEqual(reactionState.timeline[6].message, "Remove 超频");

const linkState = applyAction(
  reactionState,
  { name: "Ally 01", atkPower: 12 },
  {
    name: "Ally Link",
    actionType: "LinkAttack",
    flatDamage: 20,
    atkPowerMultiplier: 1,
    damageType: CombatDamageType.RuntimeDamage,
    tags: ["nos"],
  }
);

assert.strictEqual(linkState.target.currentHealth, 93);
assert.strictEqual(linkState.linkWindow.remaining, 0);
assert.strictEqual(linkState.targetTags[1].duration, 5);

const blockedLinkState = applyAction(
  linkState,
  { name: "Ally 02", atkPower: 10 },
  {
    name: "Blocked Link",
    actionType: "LinkAttack",
    flatDamage: 999,
    atkPowerMultiplier: 0,
    damageType: CombatDamageType.RuntimeDamage,
    tags: [],
  }
);

assert.strictEqual(blockedLinkState.target.currentHealth, 93);
assert.strictEqual(blockedLinkState.timeline[blockedLinkState.timeline.length - 1].type, "blocked");

const timeoutState = advanceTime(reactionState, 4.1);
assert.strictEqual(timeoutState.linkWindow.remaining, 0);
assert.strictEqual(timeoutState.timeline[timeoutState.timeline.length - 1].type, "link");

console.log("reaction-engine tests passed");
