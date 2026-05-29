(function (root, factory) {
  if (typeof module === "object" && module.exports) {
    module.exports = factory();
    return;
  }

  root.TagReactionEngine = factory();
})(typeof self !== "undefined" ? self : this, function () {
  function createInitialState() {
    return {
      target: createTarget("Enemy Dummy", 100),
      targetTags: [],
      rules: [],
      timeline: [],
    };
  }

  function createTarget(name, maxHealth) {
    var health = Math.max(1, parseNumber(maxHealth, 100));

    return {
      name: normalizeName(name) || "Enemy Dummy",
      maxHealth: health,
      currentHealth: health,
    };
  }

  function normalizeName(value) {
    return String(value || "").trim();
  }

  function normalizeTags(values) {
    return parseTagList(values).map(function (tag) {
      return tag.name
        + (tag.stacks > 1 ? ":" + tag.stacks : "")
        + (tag.duration > 0 ? "@" + tag.duration : "");
    });
  }

  function parseTagList(values) {
    var items = Array.isArray(values) ? values : String(values || "").split(",");
    var merged = [];

    items.forEach(function (item) {
      var tag = parseTagSpec(item);

      if (tag.name) {
        addTagStacks(merged, tag.name, tag.stacks, tag.duration);
      }
    });

    return merged;
  }

  function parseTagSpec(value) {
    if (value == null || normalizeName(value) === "") {
      return null;
    }

    if (value && typeof value === "object") {
      if (!normalizeName(value.name)) {
        return null;
      }

      return {
        name: normalizeName(value.name),
        stacks: Math.max(1, Math.floor(parseNumber(value.stacks, 1))),
        duration: Math.max(0, parseNumber(value.duration, 0)),
      };
    }

    var text = normalizeName(value);
    var durationParts = text.split("@");
    var stackParts = durationParts[0].split(":");

    return {
      name: normalizeName(stackParts[0]),
      stacks: Math.max(1, Math.floor(parseNumber(stackParts[1], 1))),
      duration: Math.max(0, parseNumber(durationParts[1], 0)),
    };
  }

  function addRule(tagA, tagB, resultTag, priority) {
    var requirementsText = tagB ? tagA + "," + tagB : tagA;

    return {
      requirements: parseTagList(requirementsText),
      result: parseTagSpec(resultTag),
      priority: Math.floor(parseNumber(priority, 0)),
    };
  }

  function cloneState(state) {
    var sourceTarget = state.target || createTarget("Enemy Dummy", 100);
    var maxHealth = Math.max(1, parseNumber(sourceTarget.maxHealth, 100));
    var currentHealth = clamp(parseNumber(sourceTarget.currentHealth, maxHealth), 0, maxHealth);

    return {
      target: {
        name: normalizeName(sourceTarget.name) || "Enemy Dummy",
        maxHealth: maxHealth,
        currentHealth: currentHealth,
      },
      targetTags: parseTagList(state.targetTags),
      rules: (state.rules || []).map(normalizeRule),
      timeline: (state.timeline || []).slice(),
    };
  }

  function normalizeRule(rule) {
    if (rule && Array.isArray(rule.requirements)) {
      var normalizedResult = parseTagSpec(rule.result);

      return {
        requirements: parseTagList(rule.requirements),
        result: normalizedResult,
        priority: Math.floor(parseNumber(rule.priority, 0)),
      };
    }

    return addRule(rule && rule.tagA, rule && rule.tagB, rule && rule.resultTag);
  }

  function applyAction(state, action) {
    var nextState = cloneState(state);
    var actionName = normalizeName(action && action.name) || "Unnamed Action";
    var actionTags = parseTagList(action && action.tags);
    var damage = Math.max(0, parseNumber(action && action.damage, 0));

    nextState.timeline.push({
      type: "action",
      message: actionName,
    });

    if (damage > 0 && nextState.target.currentHealth > 0) {
      var previousHealth = nextState.target.currentHealth;
      nextState.target.currentHealth = Math.max(0, nextState.target.currentHealth - damage);
      nextState.timeline.push({
        type: "damage",
        message: "Deal " + (previousHealth - nextState.target.currentHealth) + " damage",
      });
    }

    actionTags.forEach(function (tag) {
      addTagStacks(nextState.targetTags, tag.name, tag.stacks, tag.duration);
      nextState.timeline.push({
        type: "tag",
        message: "Apply " + formatTag(tag),
      });
    });

    resolveReactions(nextState);

    if (nextState.target.currentHealth <= 0 && !hasDeadEvent(nextState.timeline)) {
      nextState.timeline.push({
        type: "dead",
        message: nextState.target.name + " defeated",
      });
    }

    return nextState;
  }

  function setTargetHealth(state, maxHealth, currentHealth) {
    var nextState = cloneState(state);
    var nextMaxHealth = Math.max(1, parseNumber(maxHealth, nextState.target.maxHealth));
    var nextCurrentHealth = clamp(parseNumber(currentHealth, nextMaxHealth), 0, nextMaxHealth);

    nextState.target.maxHealth = nextMaxHealth;
    nextState.target.currentHealth = nextCurrentHealth;
    return nextState;
  }

  function addTagStacks(tags, name, stacks, duration) {
    var tagName = normalizeName(name);
    var stackCount = Math.max(1, Math.floor(parseNumber(stacks, 1)));
    var durationSeconds = Math.max(0, parseNumber(duration, 0));

    if (!tagName) {
      return;
    }

    var existing = findTag(tags, tagName);

    if (existing) {
      existing.stacks += stackCount;
      existing.duration = Math.max(existing.duration || 0, durationSeconds);
      return;
    }

    var tag = {
      name: tagName,
      stacks: stackCount,
    };

    if (durationSeconds > 0) {
      tag.duration = durationSeconds;
    }

    tags.push(tag);
  }

  function resolveReactions(state) {
    var guard = 0;

    while (guard < 32) {
      var reaction = findFirstReaction(state.targetTags, state.rules);

      if (!reaction) {
        return;
      }

      reaction.rule.requirements.forEach(function (requiredTag) {
        consumeTagStacks(state.targetTags, requiredTag.name, requiredTag.stacks);
      });
      if (reaction.rule.result) {
        addTagStacks(
          state.targetTags,
          reaction.rule.result.name,
          reaction.rule.result.stacks,
          reaction.rule.result.duration);
      }

      state.timeline.push({
        type: "reaction",
        message: formatRequirements(reaction.rule.requirements) + " => " + formatRuleResult(reaction.rule.result),
      });

      guard += 1;
    }

    state.timeline.push({
      type: "warning",
      message: "Reaction chain stopped: possible loop",
    });
  }

  function findFirstReaction(tags, rules) {
    var sortedRules = rules.map(normalizeRule).sort(function (left, right) {
      return right.priority - left.priority;
    });

    for (var i = 0; i < sortedRules.length; i += 1) {
      var rule = sortedRules[i];

      if (rule.requirements.length === 0) {
        continue;
      }

      if (rule.requirements.every(function (requiredTag) {
        var currentTag = findTag(tags, requiredTag.name);
        return currentTag && currentTag.stacks >= requiredTag.stacks;
      })) {
        return { rule: rule };
      }
    }

    return null;
  }

  function consumeTagStacks(tags, name, stacks) {
    var currentTag = findTag(tags, name);

    if (!currentTag) {
      return;
    }

    currentTag.stacks -= stacks;

    if (currentTag.stacks <= 0) {
      tags.splice(tags.indexOf(currentTag), 1);
    }
  }

  function findTag(tags, name) {
    return tags.find(function (tag) {
      return tag.name === name;
    });
  }

  function formatRequirements(requirements) {
    return requirements.map(formatTag).join(" + ");
  }

  function formatTag(tag) {
    return tag.name
      + (tag.stacks > 1 ? " x" + tag.stacks : "")
      + (tag.duration > 0 ? " (" + tag.duration + "s)" : "");
  }

  function formatRuleResult(result) {
    return result ? formatTag(result) : "Clear";
  }

  function parseNumber(value, fallback) {
    var number = Number(value);
    return Number.isFinite(number) ? number : fallback;
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function hasDeadEvent(timeline) {
    return timeline.some(function (event) {
      return event.type === "dead";
    });
  }

  return {
    createInitialState: createInitialState,
    createTarget: createTarget,
    normalizeTags: normalizeTags,
    parseTagList: parseTagList,
    addRule: addRule,
    applyAction: applyAction,
    setTargetHealth: setTargetHealth,
  };
});
