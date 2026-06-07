(function (root, factory) {
  if (typeof module === "object" && module.exports) {
    module.exports = factory();
    return;
  }

  root.TagReactionEngine = factory();
})(typeof self !== "undefined" ? self : this, function () {
  var CombatDamageType = {
    StructuralDamage: "StructuralDamage",
    RuntimeDamage: "RuntimeDamage",
  };

  var ReactionEffectType = {
    ApplyTag: "ApplyTag",
    RemoveTag: "RemoveTag",
    DealDamage: "DealDamage",
    SpreadTag: "SpreadTag",
    ApplyControl: "ApplyControl",
    InterruptAction: "InterruptAction",
    ModifyResource: "ModifyResource",
    CustomEvent: "CustomEvent",
  };

  var LinkWindowSeconds = 4;
  var MaxReactionDepth = 32;

  function createInitialState() {
    return {
      target: createTarget("Enemy Dummy", 100),
      tagDefinitions: [],
      targetTags: [],
      rules: [],
      linkWindow: createLinkWindow(),
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

  function createTagDefinition(id, displayName, tagLevel, defaultDuration, maxStackCount) {
    var normalizedId = normalizeName(id) || "combat.tag";

    return {
      id: normalizedId,
      displayName: normalizeName(displayName) || normalizedId,
      level: Math.max(1, Math.floor(parseNumber(tagLevel, 1))),
      defaultDuration: Math.max(0, parseNumber(defaultDuration, 0)),
      maxStackCount: Math.max(1, Math.floor(parseNumber(maxStackCount, 1))),
    };
  }

  function createLinkWindow() {
    return {
      remaining: 0,
      targetName: "",
    };
  }

  function normalizeName(value) {
    return String(value || "").trim();
  }

  function normalizeTags(values) {
    return parseTagList(values).map(formatTagSpec);
  }

  function parseTagList(values) {
    var items = Array.isArray(values) ? values : String(values || "").split(",");
    var merged = [];

    items.forEach(function (item) {
      var tag = parseTagSpec(item);

      if (tag && tag.id) {
        addParsedTag(merged, tag);
      }
    });

    return merged;
  }

  function parseTagSpec(value) {
    if (value == null || normalizeName(value) === "") {
      return null;
    }

    if (value && typeof value === "object") {
      var objectId = normalizeName(value.id || value.tag || value.name);

      if (!objectId) {
        return null;
      }

      return {
        id: objectId,
        stacks: Math.max(1, Math.floor(parseNumber(value.stacks || value.stackCount, 1))),
        duration: hasOwn(value, "duration") ? Math.max(0, parseNumber(value.duration, 0)) : null,
      };
    }

    var text = normalizeName(value);
    var durationParts = text.split("@");
    var stackParts = durationParts[0].split(":");

    return {
      id: normalizeName(stackParts[0]),
      stacks: Math.max(1, Math.floor(parseNumber(stackParts[1], 1))),
      duration: durationParts.length > 1 ? Math.max(0, parseNumber(durationParts[1], 0)) : null,
    };
  }

  function addReactionRule(options, legacyTagB, legacyResultTag, legacyPriority) {
    if (typeof options === "string") {
      var requirements = parseTagList(legacyTagB ? options + "," + legacyTagB : options);
      var effects = legacyResultTag ? [createReactionEffect(ReactionEffectType.ApplyTag, legacyResultTag)] : [];

      return {
        firstTag: requirements[0] ? requirements[0].id : "",
        secondTag: requirements[1] ? requirements[1].id : requirements[0] ? requirements[0].id : "",
        requiredFirstStack: requirements[0] ? requirements[0].stacks : 1,
        requiredSecondStack: requirements[1] ? requirements[1].stacks : requirements[0] ? requirements[0].stacks : 1,
        priority: Math.floor(parseNumber(legacyPriority, 0)),
        reactionEffects: effects,
      };
    }

    var rule = options || {};

    return {
      firstTag: normalizeName(rule.firstTag || rule.tagA),
      secondTag: normalizeName(rule.secondTag || rule.tagB || rule.firstTag || rule.tagA),
      requiredFirstStack: Math.max(1, Math.floor(parseNumber(rule.requiredFirstStack, 1))),
      requiredSecondStack: Math.max(1, Math.floor(parseNumber(rule.requiredSecondStack, 1))),
      priority: Math.floor(parseNumber(rule.priority, 0)),
      reactionEffects: normalizeReactionEffects(rule.reactionEffects || rule.effects || []),
    };
  }

  function createReactionEffect(type, tag, options) {
    var source = options || {};

    return {
      type: normalizeEffectType(type),
      tag: normalizeName(tag || source.tag),
      duration: hasOwn(source, "duration") ? Math.max(0, parseNumber(source.duration, 0)) : 0,
      stackCount: Math.max(1, Math.floor(parseNumber(source.stackCount || source.stacks, 1))),
      damageAmount: Math.max(0, Math.floor(parseNumber(source.damageAmount || source.damage, 0))),
      damageType: normalizeDamageType(source.damageType),
      value: parseNumber(source.value, 0),
      radius: Math.max(0, parseNumber(source.radius, 0)),
      effectId: normalizeName(source.effectId || source.id),
    };
  }

  function parseReactionEffects(text) {
    return String(text || "")
      .split(";")
      .map(function (part) {
        return parseReactionEffectSpec(part);
      })
      .filter(Boolean);
  }

  function parseReactionEffectSpec(value) {
    var text = normalizeName(value);

    if (!text) {
      return null;
    }

    var parts = text.split(":");
    var typeCandidate = normalizeEffectType(parts[0]);

    if (!isKnownEffectType(parts[0])) {
      return createReactionEffect(ReactionEffectType.ApplyTag, text);
    }

    if (typeCandidate === ReactionEffectType.DealDamage) {
      return createReactionEffect(typeCandidate, "", {
        damageAmount: parts[1],
        damageType: parts[2],
      });
    }

    if (typeCandidate === ReactionEffectType.CustomEvent) {
      return createReactionEffect(typeCandidate, "", {
        effectId: parts[1],
      });
    }

    var tagSpec = parseTagSpec(parts.slice(1).join(":"));
    return createReactionEffect(typeCandidate, tagSpec && tagSpec.id, {
      stackCount: tagSpec && tagSpec.stacks,
      duration: tagSpec && tagSpec.duration,
      value: parts[2],
      radius: parts[3],
    });
  }

  function normalizeReactionEffects(effects) {
    if (typeof effects === "string") {
      return parseReactionEffects(effects);
    }

    return (effects || [])
      .map(function (effect) {
        if (typeof effect === "string") {
          return parseReactionEffectSpec(effect);
        }

        return createReactionEffect(effect.type || effect.effectType, effect.tag, effect);
      })
      .filter(function (effect) {
        return effect && isValidEffect(effect);
      });
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
      tagDefinitions: (state.tagDefinitions || []).map(normalizeTagDefinition),
      targetTags: normalizeActiveTags(state.targetTags || [], state.tagDefinitions || []),
      rules: (state.rules || []).map(addReactionRule),
      linkWindow: normalizeLinkWindow(state.linkWindow),
      timeline: (state.timeline || []).slice(),
    };
  }

  function normalizeTagDefinition(definition) {
    return createTagDefinition(
      definition && (definition.id || definition.tagId || definition.name),
      definition && definition.displayName,
      definition && (definition.level || definition.tagLevel),
      definition && definition.defaultDuration,
      definition && definition.maxStackCount);
  }

  function normalizeActiveTags(tags, definitions) {
    var activeTags = [];

    parseTagList(tags).forEach(function (tag) {
      addActiveTag(activeTags, definitions, tag.id, tag.stacks, tag.duration, false);
    });

    return activeTags;
  }

  function normalizeLinkWindow(linkWindow) {
    return {
      remaining: Math.max(0, parseNumber(linkWindow && linkWindow.remaining, 0)),
      targetName: normalizeName(linkWindow && linkWindow.targetName),
    };
  }

  function applyAction(state, actor, action) {
    if (action == null) {
      action = actor;
      actor = null;
    }

    var nextState = cloneState(state);
    var normalizedActor = normalizeActor(actor);
    var normalizedAction = normalizeAction(action);

    if (normalizedAction.actionType === "LinkAttack" && nextState.linkWindow.remaining <= 0) {
      nextState.timeline.push({
        type: "blocked",
        message: normalizedAction.name + " blocked: link window closed",
      });
      return nextState;
    }

    nextState.timeline.push({
      type: "action",
      message: normalizedActor.name + " / " + normalizedAction.name,
    });

    applyActionDamage(nextState, normalizedActor, normalizedAction);
    applyActionTags(nextState, normalizedAction, normalizedActor.name);
    resolveReactions(nextState);

    if (normalizedAction.actionType === "LinkAttack") {
      closeLinkWindow(nextState);
    }

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

  function advanceTime(state, deltaTime) {
    var nextState = cloneState(state);
    var seconds = Math.max(0, parseNumber(deltaTime, 0));

    if (seconds <= 0) {
      return nextState;
    }

    for (var i = nextState.targetTags.length - 1; i >= 0; i -= 1) {
      var tag = nextState.targetTags[i];

      if (!tag.duration || tag.duration <= 0) {
        continue;
      }

      tag.duration = Math.max(0, roundTo(tag.duration - seconds, 2));

      if (tag.duration <= 0) {
        nextState.targetTags.splice(i, 1);
        nextState.timeline.push({
          type: "tag",
          message: "Expire " + getTagDisplayName(nextState, tag.id),
        });
      }
    }

    if (nextState.linkWindow.remaining > 0) {
      nextState.linkWindow.remaining = Math.max(0, roundTo(nextState.linkWindow.remaining - seconds, 2));

      if (nextState.linkWindow.remaining <= 0) {
        nextState.timeline.push({
          type: "link",
          message: "Link window closed",
        });
      }
    }

    return nextState;
  }

  function normalizeActor(actor) {
    return {
      name: normalizeName(actor && actor.name) || "Actor",
      atkPower: Math.max(0, parseNumber(actor && (actor.atkPower || actor.attackPower), 0)),
    };
  }

  function normalizeAction(action) {
    return {
      name: normalizeName(action && action.name) || "Unnamed Action",
      actionType: normalizeActionType(action && action.actionType),
      flatDamage: Math.max(0, parseNumber(action && (action.flatDamage || action.damage), 0)),
      atkPowerMultiplier: Math.max(0, parseNumber(action && action.atkPowerMultiplier, 0)),
      damageType: normalizeDamageType(action && action.damageType),
      tags: parseTagList(action && action.tags),
    };
  }

  function normalizeActionType(value) {
    var actionType = normalizeName(value);

    if (!actionType) {
      return "Skill";
    }

    if (actionType === "Link" || actionType === "LinkAttack") {
      return "LinkAttack";
    }

    return actionType;
  }

  function applyActionDamage(state, actor, action) {
    if (state.target.currentHealth <= 0) {
      return;
    }

    var damage = Math.max(0, Math.round(action.flatDamage + actor.atkPower * action.atkPowerMultiplier));

    if (damage <= 0) {
      return;
    }

    dealDamage(state, damage, action.damageType);
  }

  function applyActionTags(state, action, sourceName) {
    action.tags.forEach(function (tag) {
      var appliedTag = addActiveTag(
        state.targetTags,
        state.tagDefinitions,
        tag.id,
        tag.stacks,
        tag.duration,
        true);

      if (!appliedTag) {
        return;
      }

      state.timeline.push({
        type: "tag",
        message: "Apply " + formatActiveTag(appliedTag),
        source: sourceName,
      });
    });
  }

  function resolveReactions(state) {
    var guard = 0;

    while (guard < MaxReactionDepth) {
      var reaction = findFirstReaction(state.targetTags, state.rules);

      if (!reaction) {
        return;
      }

      state.timeline.push({
        type: "reaction",
        message: "Reaction " + reaction.rule.firstTag + " + " + reaction.rule.secondTag,
      });
      openLinkWindow(state);
      executeReactionEffects(state, reaction.rule);
      guard += 1;
    }

    state.timeline.push({
      type: "warning",
      message: "Reaction chain stopped: possible loop",
    });
  }

  function executeReactionEffects(state, rule) {
    rule.reactionEffects.forEach(function (effect) {
      if (!isValidEffect(effect)) {
        return;
      }

      switch (effect.type) {
        case ReactionEffectType.ApplyTag:
          executeApplyTagEffect(state, effect);
          break;
        case ReactionEffectType.RemoveTag:
          executeRemoveTagEffect(state, effect);
          break;
        case ReactionEffectType.DealDamage:
          executeDealDamageEffect(state, effect);
          break;
        case ReactionEffectType.SpreadTag:
        case ReactionEffectType.ApplyControl:
        case ReactionEffectType.InterruptAction:
        case ReactionEffectType.ModifyResource:
        case ReactionEffectType.CustomEvent:
          state.timeline.push({
            type: "effect",
            message: effect.type + " reserved",
          });
          break;
      }
    });
  }

  function executeApplyTagEffect(state, effect) {
    var appliedTag = addActiveTag(
      state.targetTags,
      state.tagDefinitions,
      effect.tag,
      effect.stackCount,
      effect.duration,
      true);

    if (!appliedTag) {
      return;
    }

    state.timeline.push({
      type: "tag",
      message: "Apply " + formatActiveTag(appliedTag),
    });
  }

  function executeRemoveTagEffect(state, effect) {
    var removed = removeActiveTag(state.targetTags, effect.tag);

    if (!removed) {
      return;
    }

    state.timeline.push({
      type: "tag",
      message: "Remove " + getTagDisplayName(state, removed.id),
    });
  }

  function executeDealDamageEffect(state, effect) {
    if (effect.damageAmount <= 0 || state.target.currentHealth <= 0) {
      return;
    }

    dealDamage(state, effect.damageAmount, effect.damageType);
  }

  function dealDamage(state, damageAmount, damageType) {
    var previousHealth = state.target.currentHealth;
    state.target.currentHealth = Math.max(0, state.target.currentHealth - damageAmount);

    state.timeline.push({
      type: "damage",
      message: "Deal " + (previousHealth - state.target.currentHealth) + " " + normalizeDamageType(damageType),
    });
  }

  function addActiveTag(tags, definitions, id, stacks, duration, useDefinitionDuration) {
    var tagId = normalizeName(id);

    if (!tagId) {
      return null;
    }

    var definition = findTagDefinition(definitions, tagId);
    var maxStacks = definition ? definition.maxStackCount : Number.MAX_SAFE_INTEGER;
    var displayName = definition ? definition.displayName : tagId;
    var tagLevel = definition ? definition.level : 1;
    var resolvedDuration = resolveTagDuration(definition, duration, useDefinitionDuration);
    var stackCount = Math.min(
      maxStacks,
      Math.max(1, Math.floor(parseNumber(stacks, 1))));
    var existing = findActiveTag(tags, tagId);

    if (existing) {
      existing.stacks = Math.min(maxStacks, existing.stacks + stackCount);
      existing.duration = Math.max(existing.duration || 0, resolvedDuration || 0);
      existing.displayName = displayName;
      existing.level = tagLevel;
      return existing;
    }

    var tag = {
      id: tagId,
      displayName: displayName,
      level: tagLevel,
      stacks: stackCount,
    };

    if (resolvedDuration > 0) {
      tag.duration = resolvedDuration;
    }

    tags.push(tag);
    return tag;
  }

  function addParsedTag(tags, tag) {
    var existing = findActiveTag(tags, tag.id);

    if (existing) {
      existing.stacks += tag.stacks;
      existing.duration = Math.max(existing.duration || 0, tag.duration || 0);
      return;
    }

    tags.push({
      id: tag.id,
      stacks: tag.stacks,
      duration: tag.duration,
    });
  }

  function removeActiveTag(tags, id) {
    var currentTag = findActiveTag(tags, id);

    if (!currentTag) {
      return null;
    }

    tags.splice(tags.indexOf(currentTag), 1);
    return currentTag;
  }

  function resolveTagDuration(definition, duration, useDefinitionDuration) {
    if (duration != null && duration > 0) {
      return duration;
    }

    if (useDefinitionDuration && definition && definition.defaultDuration > 0) {
      return definition.defaultDuration;
    }

    return 0;
  }

  function findFirstReaction(tags, rules) {
    var sortedRules = rules
      .map(addReactionRule)
      .filter(isValidRule)
      .sort(function (left, right) {
        return right.priority - left.priority;
      });

    for (var i = 0; i < sortedRules.length; i += 1) {
      var rule = sortedRules[i];

      if (hasRequiredStacks(tags, rule)) {
        return { rule: rule };
      }
    }

    return null;
  }

  function hasRequiredStacks(tags, rule) {
    if (rule.firstTag === rule.secondTag) {
      var sameTag = findActiveTag(tags, rule.firstTag);
      return sameTag && sameTag.stacks >= Math.max(rule.requiredFirstStack, rule.requiredSecondStack);
    }

    var firstTag = findActiveTag(tags, rule.firstTag);
    var secondTag = findActiveTag(tags, rule.secondTag);

    return firstTag
      && secondTag
      && firstTag.stacks >= rule.requiredFirstStack
      && secondTag.stacks >= rule.requiredSecondStack;
  }

  function openLinkWindow(state) {
    state.linkWindow.remaining = LinkWindowSeconds;
    state.linkWindow.targetName = state.target.name;
  }

  function closeLinkWindow(state) {
    state.linkWindow.remaining = 0;
    state.linkWindow.targetName = "";
  }

  function findTagDefinition(definitions, id) {
    var tagId = normalizeName(id);

    return (definitions || []).find(function (definition) {
      return definition && definition.id === tagId;
    });
  }

  function findActiveTag(tags, id) {
    var tagId = normalizeName(id);

    return (tags || []).find(function (tag) {
      return tag && (tag.id || tag.name) === tagId;
    });
  }

  function getTagDisplayName(state, id) {
    var activeTag = findActiveTag(state.targetTags, id);

    if (activeTag && activeTag.displayName) {
      return activeTag.displayName;
    }

    var definition = findTagDefinition(state.tagDefinitions, id);
    return definition ? definition.displayName : normalizeName(id);
  }

  function formatActiveTag(tag) {
    return (tag.displayName || tag.id)
      + (tag.stacks > 1 ? " x" + tag.stacks : "")
      + (tag.duration > 0 ? " (" + tag.duration + "s)" : "");
  }

  function formatTagSpec(tag) {
    return tag.id
      + (tag.stacks > 1 ? ":" + tag.stacks : "")
      + (tag.duration > 0 ? "@" + tag.duration : "");
  }

  function formatReactionEffect(effect) {
    if (!effect) {
      return "";
    }

    switch (effect.type) {
      case ReactionEffectType.ApplyTag:
      case ReactionEffectType.RemoveTag:
      case ReactionEffectType.SpreadTag:
        return effect.type + ":" + formatTagSpec({
          id: effect.tag,
          stacks: effect.stackCount,
          duration: effect.duration,
        });
      case ReactionEffectType.DealDamage:
        return effect.type + ":" + effect.damageAmount + ":" + effect.damageType;
      case ReactionEffectType.CustomEvent:
        return effect.type + ":" + effect.effectId;
      default:
        return effect.type;
    }
  }

  function isValidRule(rule) {
    return rule
      && rule.firstTag
      && rule.secondTag
      && rule.reactionEffects
      && rule.reactionEffects.some(isValidEffect);
  }

  function isValidEffect(effect) {
    if (!effect || !effect.type) {
      return false;
    }

    switch (effect.type) {
      case ReactionEffectType.ApplyTag:
      case ReactionEffectType.RemoveTag:
      case ReactionEffectType.SpreadTag:
        return !!effect.tag;
      case ReactionEffectType.DealDamage:
        return effect.damageAmount > 0;
      case ReactionEffectType.CustomEvent:
        return !!effect.effectId;
      default:
        return true;
    }
  }

  function isKnownEffectType(value) {
    return Object.prototype.hasOwnProperty.call(ReactionEffectType, normalizeName(value));
  }

  function normalizeEffectType(value) {
    var effectType = normalizeName(value);

    if (isKnownEffectType(effectType)) {
      return ReactionEffectType[effectType];
    }

    return ReactionEffectType.ApplyTag;
  }

  function normalizeDamageType(value) {
    var damageType = normalizeName(value);

    if (damageType === CombatDamageType.RuntimeDamage) {
      return CombatDamageType.RuntimeDamage;
    }

    return CombatDamageType.StructuralDamage;
  }

  function parseNumber(value, fallback) {
    var number = Number(value);
    return Number.isFinite(number) ? number : fallback;
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function roundTo(value, digits) {
    var scale = Math.pow(10, digits);
    return Math.round(value * scale) / scale;
  }

  function hasOwn(source, key) {
    return Object.prototype.hasOwnProperty.call(source, key);
  }

  function hasDeadEvent(timeline) {
    return timeline.some(function (event) {
      return event.type === "dead";
    });
  }

  return {
    CombatDamageType: CombatDamageType,
    ReactionEffectType: ReactionEffectType,
    LinkWindowSeconds: LinkWindowSeconds,
    createInitialState: createInitialState,
    createTarget: createTarget,
    createTagDefinition: createTagDefinition,
    createReactionEffect: createReactionEffect,
    addReactionRule: addReactionRule,
    addRule: addReactionRule,
    normalizeTags: normalizeTags,
    parseTagList: parseTagList,
    parseTagSpec: parseTagSpec,
    parseReactionEffects: parseReactionEffects,
    formatReactionEffect: formatReactionEffect,
    applyAction: applyAction,
    setTargetHealth: setTargetHealth,
    advanceTime: advanceTime,
  };
});
