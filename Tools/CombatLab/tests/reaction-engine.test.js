const assert = require("assert");
const {
  createInitialState,
  addRule,
  applyAction,
} = require("../reaction-engine");

const state = createInitialState();
state.target = {
  name: "Enemy Dummy",
  maxHealth: 100,
  currentHealth: 100,
};
state.targetTags = ["Break"];
state.rules = [
  addRule("Break", "Shock", "Stun"),
];

const nextState = applyAction(state, {
  name: "Test Shock Hit",
  damage: 25,
  tags: ["Shock"],
});

assert.strictEqual(nextState.target.currentHealth, 75);
assert.deepStrictEqual(nextState.targetTags, [
  { name: "Stun", stacks: 1 },
]);
assert.strictEqual(nextState.timeline.length, 4);
assert.strictEqual(nextState.timeline[0].type, "action");
assert.strictEqual(nextState.timeline[1].type, "damage");
assert.strictEqual(nextState.timeline[1].message, "Deal 25 damage");
assert.strictEqual(nextState.timeline[2].type, "tag");
assert.strictEqual(nextState.timeline[3].type, "reaction");
assert.strictEqual(nextState.timeline[3].message, "Break + Shock => Stun");

const lethalState = applyAction(nextState, {
  name: "Finisher",
  damage: 999,
  tags: [],
});

assert.strictEqual(lethalState.target.currentHealth, 0);
assert.strictEqual(lethalState.timeline[lethalState.timeline.length - 1].type, "dead");

const stackState = createInitialState();
stackState.rules = [
  addRule("Fire:3", "", "Burning"),
];

const burnState = applyAction(stackState, {
  name: "Triple Fire",
  damage: 0,
  tags: ["Fire:3"],
});

assert.deepStrictEqual(burnState.targetTags, [
  { name: "Burning", stacks: 1 },
]);
assert.strictEqual(burnState.timeline[burnState.timeline.length - 1].message, "Fire x3 => Burning");

const durationState = applyAction(createInitialState(), {
  name: "Timed Fire",
  damage: 0,
  tags: [
    { name: "Fire", stacks: 1, duration: 2.5 },
    { name: "Fire", stacks: 2, duration: 1.5 },
  ],
});

assert.deepStrictEqual(durationState.targetTags, [
  { name: "Fire", stacks: 3, duration: 2.5 },
]);

const priorityState = createInitialState();
priorityState.targetTags = [
  { name: "Break", stacks: 1, duration: 4 },
  { name: "Shock", stacks: 1, duration: 3 },
  { name: "Fire", stacks: 3, duration: 5 },
];
priorityState.rules = [
  { requirements: [{ name: "Break", stacks: 1 }, { name: "Shock", stacks: 1 }], result: { name: "Stun", stacks: 1 }, priority: 1 },
  { requirements: [{ name: "Fire", stacks: 3 }], result: { name: "Burning", stacks: 1, duration: 6 }, priority: 10 },
];

const priorityResult = applyAction(priorityState, {
  name: "Priority Probe",
  damage: 0,
  tags: [],
});

assert.strictEqual(priorityResult.timeline[1].message, "Fire x3 => Burning (6s)");
assert.deepStrictEqual(priorityResult.targetTags, [
  { name: "Burning", stacks: 1, duration: 6 },
  { name: "Stun", stacks: 1 },
]);

const consumeOnlyState = createInitialState();
consumeOnlyState.targetTags = [
  { name: "Shield", stacks: 3, duration: 8 },
];
consumeOnlyState.rules = [
  { requirements: [{ name: "Shield", stacks: 3 }], result: null, priority: 1 },
];

const consumeOnlyResult = applyAction(consumeOnlyState, {
  name: "Strip Shield",
  damage: 0,
  tags: [],
});

assert.deepStrictEqual(consumeOnlyResult.targetTags, []);
assert.strictEqual(consumeOnlyResult.timeline[1].message, "Shield x3 => Clear");

console.log("reaction-engine tests passed");
