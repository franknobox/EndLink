(function () {
  var engine = window.TagReactionEngine;
  var state = engine.createInitialState();
  var characters = [];
  var linkTimerId = 0;

  var sampleTagDefinitions = [
    engine.createTagDefinition("ovl", "超频", 1, 6, 3),
    engine.createTagDefinition("nos", "噪声", 1, 5, 3),
    engine.createTagDefinition("stm", "流转", 1, 5, 1),
    engine.createTagDefinition("frz", "冻结", 1, 4, 1),
    engine.createTagDefinition("cps", "压缩", 1, 4, 3),
    engine.createTagDefinition("uld", "卸载", 1, 5, 3),
    engine.createTagDefinition("meltdown", "核心熔毁", 2, 4, 1),
    engine.createTagDefinition("deadlock", "死锁", 2, 4, 1),
  ];

  var sampleCharacters = [
    {
      name: "Player",
      atkPower: 20,
      skill: {
        flatDamage: 8,
        atkPowerMultiplier: 1.2,
        damageType: engine.CombatDamageType.StructuralDamage,
        tags: "ovl:2",
      },
      link: {
        flatDamage: 18,
        atkPowerMultiplier: 1.6,
        damageType: engine.CombatDamageType.RuntimeDamage,
        tags: "nos",
      },
    },
    {
      name: "Ally 01",
      atkPower: 14,
      skill: {
        flatDamage: 6,
        atkPowerMultiplier: 1,
        damageType: engine.CombatDamageType.StructuralDamage,
        tags: "ovl",
      },
      link: {
        flatDamage: 14,
        atkPowerMultiplier: 1.4,
        damageType: engine.CombatDamageType.RuntimeDamage,
        tags: "stm",
      },
    },
    {
      name: "Ally 02",
      atkPower: 16,
      skill: {
        flatDamage: 7,
        atkPowerMultiplier: 1.1,
        damageType: engine.CombatDamageType.StructuralDamage,
        tags: "uld",
      },
      link: {
        flatDamage: 16,
        atkPowerMultiplier: 1.5,
        damageType: engine.CombatDamageType.RuntimeDamage,
        tags: "frz",
      },
    },
  ];

  var elements = {
    tagDefinitionForm: document.getElementById("tagDefinitionForm"),
    tagDefinitionId: document.getElementById("tagDefinitionId"),
    tagDefinitionName: document.getElementById("tagDefinitionName"),
    tagDefinitionLevel: document.getElementById("tagDefinitionLevel"),
    tagDefinitionDuration: document.getElementById("tagDefinitionDuration"),
    tagDefinitionMaxStack: document.getElementById("tagDefinitionMaxStack"),
    tagDefinitionList: document.getElementById("tagDefinitionList"),
    ruleForm: document.getElementById("ruleForm"),
    ruleFirstTag: document.getElementById("ruleFirstTag"),
    ruleFirstStack: document.getElementById("ruleFirstStack"),
    ruleSecondTag: document.getElementById("ruleSecondTag"),
    ruleSecondStack: document.getElementById("ruleSecondStack"),
    rulePriority: document.getElementById("rulePriority"),
    ruleEffects: document.getElementById("ruleEffects"),
    rulesList: document.getElementById("rulesList"),
    targetNameLabel: document.getElementById("targetNameLabel"),
    targetHpText: document.getElementById("targetHpText"),
    targetHpFill: document.getElementById("targetHpFill"),
    targetMaxHealth: document.getElementById("targetMaxHealth"),
    targetCurrentHealth: document.getElementById("targetCurrentHealth"),
    targetTags: document.getElementById("targetTags"),
    tagForm: document.getElementById("tagForm"),
    tagInput: document.getElementById("tagInput"),
    characterBoard: document.getElementById("characterBoard"),
    characterTemplate: document.getElementById("characterTemplate"),
    linkStatus: document.getElementById("linkStatus"),
    timeline: document.getElementById("timeline"),
    resetButton: document.getElementById("resetButton"),
  };

  function loadSample() {
    state = engine.createInitialState();
    state.target = engine.createTarget("Enemy Dummy", 260);
    state.tagDefinitions = cloneDefinitions(sampleTagDefinitions);
    state.rules = [
      engine.addReactionRule({
        firstTag: "ovl",
        secondTag: "ovl",
        requiredFirstStack: 3,
        requiredSecondStack: 3,
        priority: 10,
        reactionEffects: [
          "ApplyTag:meltdown",
          "DealDamage:35:RuntimeDamage",
          "RemoveTag:ovl",
        ],
      }),
      engine.addReactionRule({
        firstTag: "frz",
        secondTag: "nos",
        requiredFirstStack: 1,
        requiredSecondStack: 1,
        priority: 8,
        reactionEffects: [
          "ApplyTag:deadlock",
          "DealDamage:25:RuntimeDamage",
          "RemoveTag:frz",
          "RemoveTag:nos",
        ],
      }),
      engine.addReactionRule({
        firstTag: "stm",
        secondTag: "ovl",
        requiredFirstStack: 1,
        requiredSecondStack: 1,
        priority: 4,
        reactionEffects: [
          "SpreadTag:ovl",
          "CustomEvent:stream_cascade",
        ],
      }),
    ];
    state.timeline = [];
    state.linkWindow = { remaining: 0, targetName: "" };
    characters = cloneCharacters(sampleCharacters);
    render();
  }

  function resetTarget() {
    state = engine.setTargetHealth(state, state.target.maxHealth, state.target.maxHealth);
    state.targetTags = [];
    state.linkWindow = { remaining: 0, targetName: "" };
    state.timeline = [];
    renderDynamic();
    renderCharacters();
  }

  function syncTargetFromInputs() {
    state = engine.setTargetHealth(
      state,
      elements.targetMaxHealth.value,
      elements.targetCurrentHealth.value
    );
    renderDynamic();
  }

  function executeCharacterAction(characterIndex, actionKey) {
    syncCharactersFromInputs();

    var character = characters[characterIndex];
    var action = character[actionKey];
    var actionType = actionKey === "link" ? "LinkAttack" : "Skill";

    state = engine.applyAction(
      state,
      {
        name: character.name,
        atkPower: character.atkPower,
      },
      {
        name: actionType,
        actionType: actionType,
        flatDamage: action.flatDamage,
        atkPowerMultiplier: action.atkPowerMultiplier,
        damageType: action.damageType,
        tags: action.tags,
      }
    );

    renderDynamic();
    renderCharacters();
  }

  function addTargetTag(tag) {
    state = engine.applyAction(
      state,
      { name: "Debug", atkPower: 0 },
      {
        name: "Manual Tag",
        actionType: "Skill",
        flatDamage: 0,
        atkPowerMultiplier: 0,
        damageType: engine.CombatDamageType.RuntimeDamage,
        tags: tag,
      }
    );

    renderDynamic();
  }

  function removeTargetTag(tagId) {
    state.targetTags = state.targetTags.filter(function (currentTag) {
      return currentTag.id !== tagId;
    });
    renderDynamic();
  }

  function saveTagDefinition() {
    var definition = engine.createTagDefinition(
      elements.tagDefinitionId.value,
      elements.tagDefinitionName.value,
      elements.tagDefinitionLevel.value,
      elements.tagDefinitionDuration.value,
      elements.tagDefinitionMaxStack.value
    );

    if (!definition.id) {
      return;
    }

    state.tagDefinitions = state.tagDefinitions.filter(function (currentDefinition) {
      return currentDefinition.id !== definition.id;
    });
    state.tagDefinitions.push(definition);
    clearTagDefinitionInputs();
    renderDefinitions();
    renderDynamic();
  }

  function addReactionRule() {
    var effects = engine.parseReactionEffects(elements.ruleEffects.value);

    if (effects.length === 0) {
      return;
    }

    var rule = engine.addReactionRule({
      firstTag: elements.ruleFirstTag.value,
      secondTag: elements.ruleSecondTag.value || elements.ruleFirstTag.value,
      requiredFirstStack: elements.ruleFirstStack.value,
      requiredSecondStack: elements.ruleSecondStack.value,
      priority: elements.rulePriority.value,
      reactionEffects: effects,
    });

    if (!rule.firstTag || !rule.secondTag) {
      return;
    }

    state.rules.push(rule);
    clearRuleInputs();
    renderRules();
  }

  function removeRule(index) {
    state.rules.splice(index, 1);
    renderRules();
  }

  function syncCharactersFromInputs() {
    var rows = Array.from(elements.characterBoard.querySelectorAll(".actor-row"));

    rows.forEach(function (row, index) {
      characters[index] = {
        name: row.querySelector(".character-name").value || "Actor " + (index + 1),
        atkPower: Number(row.querySelector(".actor-atk").value) || 0,
        skill: readActionInputs(row, "skill"),
        link: readActionInputs(row, "link"),
      };
    });
  }

  function readActionInputs(row, prefix) {
    return {
      flatDamage: Number(row.querySelector("." + prefix + "-flat").value) || 0,
      atkPowerMultiplier: Number(row.querySelector("." + prefix + "-multiplier").value) || 0,
      damageType: row.querySelector("." + prefix + "-damage-type").value,
      tags: row.querySelector("." + prefix + "-tags").value,
    };
  }

  function render() {
    renderDefinitions();
    renderRules();
    renderCharacters();
    renderDynamic();
  }

  function renderDynamic() {
    renderTarget();
    renderTags();
    renderLinkStatus();
    renderTimeline();
  }

  function renderTarget() {
    var target = state.target;
    var ratio = target.maxHealth > 0 ? target.currentHealth / target.maxHealth : 0;

    elements.targetNameLabel.textContent = target.name;
    elements.targetHpText.textContent = target.currentHealth + " / " + target.maxHealth;
    elements.targetHpFill.style.width = Math.round(ratio * 100) + "%";
    elements.targetMaxHealth.value = target.maxHealth;
    elements.targetCurrentHealth.value = target.currentHealth;
  }

  function renderTags() {
    elements.targetTags.innerHTML = "";

    if (state.targetTags.length === 0) {
      var empty = document.createElement("span");
      empty.className = "empty-text";
      empty.textContent = "None";
      elements.targetTags.appendChild(empty);
      return;
    }

    state.targetTags.forEach(function (tag) {
      var chip = document.createElement("span");
      chip.className = "tag-chip";
      chip.textContent = formatTag(tag);

      var removeButton = document.createElement("button");
      removeButton.type = "button";
      removeButton.textContent = "x";
      removeButton.setAttribute("aria-label", "Remove " + tag.id);
      removeButton.addEventListener("click", function () {
        removeTargetTag(tag.id);
      });

      chip.appendChild(removeButton);
      elements.targetTags.appendChild(chip);
    });
  }

  function renderDefinitions() {
    elements.tagDefinitionList.innerHTML = "";

    state.tagDefinitions.forEach(function (definition) {
      var item = document.createElement("button");
      item.type = "button";
      item.className = "definition-chip";
      item.textContent = definition.id
        + " / Lv."
        + definition.level
        + " / max "
        + definition.maxStackCount
        + (definition.defaultDuration > 0 ? " / " + definition.defaultDuration + "s" : "");
      item.addEventListener("click", function () {
        fillTagDefinitionInputs(definition);
      });

      elements.tagDefinitionList.appendChild(item);
    });
  }

  function renderRules() {
    elements.rulesList.innerHTML = "";

    state.rules
      .map(function (rule, index) {
        return {
          rule: rule,
          index: index,
        };
      })
      .sort(function (left, right) {
        return right.rule.priority - left.rule.priority;
      })
      .forEach(function (entry) {
        var chip = document.createElement("span");
        chip.className = "rule-chip";
        chip.textContent = formatRule(entry.rule);

        var removeButton = document.createElement("button");
        removeButton.type = "button";
        removeButton.textContent = "x";
        removeButton.setAttribute("aria-label", "Remove rule");
        removeButton.addEventListener("click", function () {
          removeRule(entry.index);
        });

        chip.appendChild(removeButton);
        elements.rulesList.appendChild(chip);
      });
  }

  function renderCharacters() {
    elements.characterBoard.innerHTML = "";

    characters.forEach(function (character, index) {
      var fragment = elements.characterTemplate.content.cloneNode(true);
      var row = fragment.querySelector(".actor-row");

      row.querySelector(".character-name").value = character.name;
      row.querySelector(".actor-atk").value = character.atkPower;
      writeActionInputs(row, "skill", character.skill);
      writeActionInputs(row, "link", character.link);

      row.querySelector(".skill-button").addEventListener("click", function () {
        executeCharacterAction(index, "skill");
      });
      row.querySelector(".link-button").addEventListener("click", function () {
        executeCharacterAction(index, "link");
      });

      row.querySelector(".link-button").disabled = state.linkWindow.remaining <= 0;
      elements.characterBoard.appendChild(fragment);
    });
  }

  function writeActionInputs(row, prefix, action) {
    row.querySelector("." + prefix + "-flat").value = action.flatDamage;
    row.querySelector("." + prefix + "-multiplier").value = action.atkPowerMultiplier;
    row.querySelector("." + prefix + "-damage-type").value = action.damageType;
    row.querySelector("." + prefix + "-tags").value = action.tags;
  }

  function renderLinkStatus() {
    if (state.linkWindow.remaining <= 0) {
      elements.linkStatus.textContent = "Closed";
      elements.linkStatus.className = "link-status";
      return;
    }

    elements.linkStatus.textContent = "Open "
      + state.linkWindow.remaining.toFixed(1)
      + "s / "
      + (state.linkWindow.targetName || state.target.name);
    elements.linkStatus.className = "link-status link-status-open";
  }

  function renderTimeline() {
    elements.timeline.innerHTML = "";

    if (state.timeline.length === 0) {
      var emptyItem = document.createElement("li");
      emptyItem.className = "timeline-empty";
      emptyItem.textContent = "No combat events";
      elements.timeline.appendChild(emptyItem);
      return;
    }

    state.timeline.slice().reverse().forEach(function (event) {
      var item = document.createElement("li");
      item.className = "event-" + event.type;

      var type = document.createElement("strong");
      type.textContent = event.type;

      var message = document.createElement("span");
      message.textContent = event.message;

      item.appendChild(type);
      item.appendChild(message);
      elements.timeline.appendChild(item);
    });
  }

  function fillTagDefinitionInputs(definition) {
    elements.tagDefinitionId.value = definition.id;
    elements.tagDefinitionName.value = definition.displayName;
    elements.tagDefinitionLevel.value = definition.level;
    elements.tagDefinitionDuration.value = definition.defaultDuration;
    elements.tagDefinitionMaxStack.value = definition.maxStackCount;
  }

  function clearTagDefinitionInputs() {
    elements.tagDefinitionId.value = "";
    elements.tagDefinitionName.value = "";
    elements.tagDefinitionLevel.value = 1;
    elements.tagDefinitionDuration.value = 0;
    elements.tagDefinitionMaxStack.value = 1;
  }

  function clearRuleInputs() {
    elements.ruleFirstTag.value = "";
    elements.ruleFirstStack.value = 1;
    elements.ruleSecondTag.value = "";
    elements.ruleSecondStack.value = 1;
    elements.rulePriority.value = 0;
    elements.ruleEffects.value = "";
  }

  function cloneDefinitions(source) {
    return source.map(function (definition) {
      return engine.createTagDefinition(
        definition.id,
        definition.displayName,
        definition.level,
        definition.defaultDuration,
        definition.maxStackCount);
    });
  }

  function cloneCharacters(source) {
    return source.map(function (character) {
      return {
        name: character.name,
        atkPower: character.atkPower,
        skill: cloneAction(character.skill),
        link: cloneAction(character.link),
      };
    });
  }

  function cloneAction(action) {
    return {
      flatDamage: action.flatDamage,
      atkPowerMultiplier: action.atkPowerMultiplier,
      damageType: action.damageType,
      tags: action.tags,
    };
  }

  function formatTag(tag) {
    return (tag.displayName || tag.id)
      + (tag.stacks > 1 ? " x" + tag.stacks : "")
      + (tag.duration > 0 ? " (" + tag.duration + "s)" : "");
  }

  function formatRule(rule) {
    return rule.firstTag
      + " x"
      + rule.requiredFirstStack
      + " + "
      + rule.secondTag
      + " x"
      + rule.requiredSecondStack
      + " / P"
      + rule.priority
      + " / "
      + rule.reactionEffects.map(engine.formatReactionEffect).join("; ");
  }

  elements.tagDefinitionForm.addEventListener("submit", function (event) {
    event.preventDefault();
    saveTagDefinition();
  });

  elements.ruleForm.addEventListener("submit", function (event) {
    event.preventDefault();
    addReactionRule();
  });

  elements.tagForm.addEventListener("submit", function (event) {
    event.preventDefault();
    addTargetTag(elements.tagInput.value);
    elements.tagInput.value = "";
  });

  elements.targetMaxHealth.addEventListener("change", syncTargetFromInputs);
  elements.targetCurrentHealth.addEventListener("change", syncTargetFromInputs);
  elements.resetButton.addEventListener("click", resetTarget);

  linkTimerId = window.setInterval(function () {
    if (state.linkWindow.remaining <= 0 && !state.targetTags.some(function (tag) {
      return tag.duration > 0;
    })) {
      return;
    }

    var wasLinkOpen = state.linkWindow.remaining > 0;
    state = engine.advanceTime(state, 0.25);
    var isLinkOpen = state.linkWindow.remaining > 0;

    renderDynamic();

    if (wasLinkOpen !== isLinkOpen) {
      renderCharacters();
    }
  }, 250);

  window.addEventListener("beforeunload", function () {
    window.clearInterval(linkTimerId);
  });

  loadSample();
})();
