(function () {
  var engine = window.TagReactionEngine;
  var state = engine.createInitialState();
  var characters = [];

  var sampleCharacters = [
    {
      name: "Player",
      skill: { damage: 18, tags: "Fire:2" },
      link: { damage: 32, tags: "Launch" },
    },
    {
      name: "Ally 01",
      skill: { damage: 14, tags: "Fire" },
      link: { damage: 30, tags: "Stun" },
    },
    {
      name: "Ally 02",
      skill: { damage: 16, tags: "Break" },
      link: { damage: 28, tags: "Wind" },
    },
  ];

  var elements = {
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
    ruleForm: document.getElementById("ruleForm"),
    ruleRequirements: document.getElementById("ruleRequirements"),
    ruleResult: document.getElementById("ruleResult"),
    rulesList: document.getElementById("rulesList"),
    timeline: document.getElementById("timeline"),
    resetButton: document.getElementById("resetButton"),
  };

  function loadSample() {
    state = engine.createInitialState();
    state.target = engine.createTarget("Enemy Dummy", 200);
    state.targetTags = [];
    state.rules = [
      engine.addRule("Fire:3", "", "Burning@6", 10),
      engine.addRule("Break, Shock", "", "Stun@4", 5),
      engine.addRule("Stun, Launch", "", "Airborne@3", 20),
      engine.addRule("Shield:3", "", "", 2),
    ];
    state.timeline = [];
    characters = cloneCharacters(sampleCharacters);
    render();
  }

  function resetTarget() {
    state = engine.setTargetHealth(state, state.target.maxHealth, state.target.maxHealth);
    state.targetTags = [];
    state.timeline = [];
    render();
  }

  function syncTargetFromInputs() {
    state = engine.setTargetHealth(
      state,
      elements.targetMaxHealth.value,
      elements.targetCurrentHealth.value
    );
    render();
  }

  function executeCharacterAction(characterIndex, actionType) {
    syncCharactersFromInputs();

    var character = characters[characterIndex];
    var action = character[actionType];
    var actionLabel = actionType === "skill" ? "Skill" : "Link";

    state = engine.applyAction(state, {
      name: character.name + " " + actionLabel,
      damage: action.damage,
      tags: action.tags,
    });
    render();
  }

  function addTargetTag(tag) {
    var parsedTags = engine.parseTagList([tag]);

    parsedTags.forEach(function (parsedTag) {
      state = engine.applyAction(state, {
        name: "Manual Tag",
        damage: 0,
        tags: [parsedTag],
      });
    });

    render();
  }

  function removeTargetTag(tagName) {
    state.targetTags = state.targetTags.filter(function (currentTag) {
      return currentTag.name !== tagName;
    });
    render();
  }

  function addReactionRule(requirements, resultTag) {
    var rule = engine.addRule(requirements, "", resultTag);

    if (rule.requirements.length === 0) {
      return;
    }

    state.rules.push(rule);
    render();
  }

  function removeRule(index) {
    state.rules.splice(index, 1);
    render();
  }

  function syncCharactersFromInputs() {
    var cards = Array.from(elements.characterBoard.querySelectorAll(".character-card"));

    cards.forEach(function (card, index) {
      characters[index] = {
        name: card.querySelector(".character-name").value || "Actor " + (index + 1),
        skill: {
          damage: Number(card.querySelector(".skill-damage").value) || 0,
          tags: card.querySelector(".skill-tags").value,
        },
        link: {
          damage: Number(card.querySelector(".link-damage").value) || 0,
          tags: card.querySelector(".link-tags").value,
        },
      };
    });
  }

  function render() {
    renderTarget();
    renderTags();
    renderRules();
    renderTimeline();
    renderCharacters();
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
      removeButton.setAttribute("aria-label", "Remove " + tag.name);
      removeButton.addEventListener("click", function () {
        removeTargetTag(tag.name);
      });

      chip.appendChild(removeButton);
      elements.targetTags.appendChild(chip);
    });
  }

  function renderCharacters() {
    elements.characterBoard.innerHTML = "";

    characters.forEach(function (character, index) {
      var fragment = elements.characterTemplate.content.cloneNode(true);
      var card = fragment.querySelector(".actor-row");

      card.querySelector(".character-name").value = character.name;
      card.querySelector(".skill-damage").value = character.skill.damage;
      card.querySelector(".skill-tags").value = character.skill.tags;
      card.querySelector(".link-damage").value = character.link.damage;
      card.querySelector(".link-tags").value = character.link.tags;

      card.querySelector(".skill-button").addEventListener("click", function () {
        executeCharacterAction(index, "skill");
      });
      card.querySelector(".link-button").addEventListener("click", function () {
        executeCharacterAction(index, "link");
      });

      elements.characterBoard.appendChild(fragment);
    });
  }

  function renderRules() {
    elements.rulesList.innerHTML = "";

    state.rules.forEach(function (rule, index) {
      var chip = document.createElement("span");
      chip.className = "rule-chip";
      chip.textContent = formatRequirements(rule.requirements)
        + " => "
        + formatRuleResult(rule.result)
        + "  P"
        + rule.priority;

      var removeButton = document.createElement("button");
      removeButton.type = "button";
      removeButton.textContent = "x";
      removeButton.setAttribute("aria-label", "Remove rule");
      removeButton.addEventListener("click", function () {
        removeRule(index);
      });

      chip.appendChild(removeButton);
      elements.rulesList.appendChild(chip);
    });
  }

  function renderTimeline() {
    elements.timeline.innerHTML = "";

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

  function cloneCharacters(source) {
    return source.map(function (character) {
      return {
        name: character.name,
        skill: {
          damage: character.skill.damage,
          tags: character.skill.tags,
        },
        link: {
          damage: character.link.damage,
          tags: character.link.tags,
        },
      };
    });
  }

  elements.tagForm.addEventListener("submit", function (event) {
    event.preventDefault();
    addTargetTag(elements.tagInput.value);
    elements.tagInput.value = "";
  });

  elements.ruleForm.addEventListener("submit", function (event) {
    event.preventDefault();
    addReactionRule(elements.ruleRequirements.value, elements.ruleResult.value);
    elements.ruleRequirements.value = "";
    elements.ruleResult.value = "";
  });

  elements.targetMaxHealth.addEventListener("change", syncTargetFromInputs);
  elements.targetCurrentHealth.addEventListener("change", syncTargetFromInputs);
  elements.resetButton.addEventListener("click", resetTarget);

  loadSample();

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
})();
