using System;
using System.Collections.Generic;
using System.Linq;
using EndLink.Combat;
using EndLink.Core;
using EndLink.Enemies;
using EndLink.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EndLink.Editor
{
    /// <summary>
    /// 当前活动场景的只读配置体检器。
    /// 检查项优先覆盖会直接阻断白模试玩或造成静默失效的问题，不对场景做任何修改。
    /// </summary>
    internal static class SceneValidator
    {
        private static readonly string[] RecommendedLayers =
        {
            "Player",
            "Enemy",
            "Ally",
            "Interactable",
            "Environment"
        };

        public static IReadOnlyList<SceneValidationIssue> ValidateActiveScene()
        {
            List<SceneValidationIssue> issues = new();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "SCENE_NOT_LOADED",
                    "场景",
                    "当前没有可体检的已加载活动场景。",
                    null);
                return issues;
            }

            List<GameObject> sceneObjects = GetSceneObjects(scene);
            HashSet<CombatActionDefinition> referencedActions = new();

            ValidateGeneral(sceneObjects, issues);
            ValidateLayers(issues);
            ValidatePlayer(sceneObjects, issues, referencedActions);
            ValidateCamera(sceneObjects, issues);
            ValidateCombat(sceneObjects, issues);
            ValidateEnemies(sceneObjects, issues, referencedActions);
            CollectActionReferences(sceneObjects, referencedActions);
            ValidateActions(referencedActions, issues);
            ValidateNavMesh(sceneObjects, issues);

            issues.Sort(CompareIssues);
            return issues;
        }

        private static List<GameObject> GetSceneObjects(Scene scene)
        {
            List<GameObject> objects = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    objects.Add(child.gameObject);
                }
            }

            return objects;
        }

        private static void ValidateGeneral(
            IReadOnlyList<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            foreach (GameObject sceneObject in sceneObjects)
            {
                int missingScriptCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(sceneObject);
                if (missingScriptCount > 0)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "GEN_MISSING_SCRIPT",
                        "通用",
                        $"物体存在 {missingScriptCount} 个 Missing Script。",
                        sceneObject);
                }

                Component[] components = sceneObject.GetComponents<Component>();
                ValidateDuplicateComponents(components, sceneObject, issues);

                foreach (MonoBehaviour behaviour in components.OfType<MonoBehaviour>())
                {
                    ValidateBrokenSerializedReferences(behaviour, issues);
                }
            }
        }

        private static void ValidateDuplicateComponents(
            IEnumerable<Component> components,
            GameObject owner,
            ICollection<SceneValidationIssue> issues)
        {
            foreach (IGrouping<Type, Component> group in components
                         .Where(component => component != null)
                         .GroupBy(component => component.GetType()))
            {
                Type componentType = group.Key;
                if (group.Count() <= 1
                    || !Attribute.IsDefined(
                        componentType,
                        typeof(DisallowMultipleComponent),
                        true))
                {
                    continue;
                }

                Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "GEN_DUPLICATE_COMPONENT",
                    "通用",
                    $"不允许重复的组件 {componentType.Name} 被挂载了 {group.Count()} 次。",
                    owner);
            }
        }

        private static void ValidateBrokenSerializedReferences(
            MonoBehaviour behaviour,
            ICollection<SceneValidationIssue> issues)
        {
            try
            {
                SerializedObject serializedObject = new(behaviour);
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.propertyPath == "m_Script"
                        || property.objectReferenceValue != null
                        || property.objectReferenceInstanceIDValue == 0)
                    {
                        continue;
                    }

                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "GEN_BROKEN_REFERENCE",
                        "通用",
                        $"{behaviour.GetType().Name} 的字段 {property.displayName} 引用了已丢失对象。",
                        behaviour);
                }
            }
            catch (Exception exception)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "GEN_SERIALIZE_CHECK_FAILED",
                    "通用",
                    $"无法检查 {behaviour.GetType().Name} 的序列化引用：{exception.Message}",
                    behaviour);
            }
        }

        private static void ValidateLayers(ICollection<SceneValidationIssue> issues)
        {
            foreach (string layerName in RecommendedLayers)
            {
                if (LayerMask.NameToLayer(layerName) >= 0)
                {
                    continue;
                }

                Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "LAYER_MISSING",
                    "Layer",
                    $"建议 Layer 未建立：{layerName}。依赖该 Layer 的筛选和碰撞配置可能静默失效。",
                    null);
            }
        }

        private static void ValidatePlayer(
            IReadOnlyList<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues,
            ISet<CombatActionDefinition> referencedActions)
        {
            List<PlayerStateMachine> players = GetActiveComponents<PlayerStateMachine>(sceneObjects);
            if (players.Count == 0)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "PLAYER_MISSING",
                    "玩家",
                    "活动场景中没有 PlayerStateMachine，无法建立主角状态流程。",
                    null);
                return;
            }

            if (players.Count > 1)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "PLAYER_MULTIPLE",
                    "玩家",
                    $"场景中存在 {players.Count} 个 PlayerStateMachine，请确认只有一个固定主控。",
                    players[0]);
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            foreach (PlayerStateMachine player in players)
            {
                GameObject root = player.gameObject;
                Require<PlayerInputReader>(root, issues, "PLAYER_INPUT_MISSING", "主角缺少 PlayerInputReader。", true);
                Require<PlayerController>(root, issues, "PLAYER_CONTROLLER_MISSING", "主角缺少 PlayerController。", true);
                Require<PlayerCombatDriver>(root, issues, "PLAYER_COMBAT_MISSING", "主角缺少 PlayerCombatDriver。", true);
                Require<CombatTarget>(root, issues, "PLAYER_TARGET_MISSING", "主角缺少 CombatTarget。", true);
                Require<CharacterHealth>(root, issues, "PLAYER_HEALTH_CORE_MISSING", "主角缺少 CharacterHealth。", true);
                Require<PlayerHealth>(root, issues, "PLAYER_HEALTH_BRIDGE_MISSING", "主角缺少 PlayerHealth 状态机桥接。", false);
                Require<PlayerTargeting>(root, issues, "PLAYER_TARGETING_MISSING", "主角缺少 PlayerTargeting，软锁与硬锁不会工作。", false);
                Require<PlayerComboController>(root, issues, "PLAYER_COMBO_MISSING", "主角缺少 PlayerComboController，连段与攻击位移不会工作。", false);
                Require<PlayerAimController>(root, issues, "PLAYER_AIM_MISSING", "主角缺少 PlayerAimController，B 形态无法进入射击瞄准。", false);
                Require<PlayerGuardController>(root, issues, "PLAYER_GUARD_MISSING", "主角缺少 PlayerGuardController，格挡与弹反不会工作。", false);
                Require<PlayerAnimatorDriver>(root, issues, "PLAYER_ANIMATOR_MISSING", "主角缺少 PlayerAnimatorDriver，动作与 Animator 无法接线。", false);

                PlayerWeaponController weaponController = root.GetComponent<PlayerWeaponController>();
                if (weaponController == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "PLAYER_WEAPON_MISSING",
                        "玩家",
                        "主角缺少 PlayerWeaponController，三种武器形态动作组尚未接入。",
                        root);
                }
                else
                {
                    ValidateWeaponActions(weaponController, issues, referencedActions);
                }

                if (playerLayer >= 0 && root.layer != playerLayer)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "PLAYER_LAYER_INVALID",
                        "Layer",
                        $"主角根物体应位于 Player Layer，当前为 {LayerMask.LayerToName(root.layer)}。",
                        root);
                }
            }
        }

        private static void ValidateWeaponActions(
            PlayerWeaponController controller,
            ICollection<SceneValidationIssue> issues,
            ISet<CombatActionDefinition> referencedActions)
        {
            foreach (PlayerWeaponForm form in Enum.GetValues(typeof(PlayerWeaponForm)))
            {
                PlayerWeaponActionSet actionSet = controller.GetActionSet(form);
                if (actionSet == null || actionSet.ComboCount == 0 || actionSet.BasicAttackAction == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "WEAPON_BASIC_ACTION_MISSING",
                        "战斗",
                        $"武器 {form} 形态没有可用的第一段普攻动作。",
                        controller);
                }

                if (actionSet == null)
                {
                    continue;
                }

                for (int index = 0; index < actionSet.ComboCount; index++)
                {
                    CombatActionDefinition comboAction = actionSet.GetComboAction(index);
                    if (comboAction == null)
                    {
                        Add(
                            issues,
                            SceneValidationSeverity.Warning,
                            "WEAPON_COMBO_SLOT_EMPTY",
                            "战斗",
                            $"武器 {form} 形态的第 {index + 1} 个连段槽为空。",
                            controller);
                        continue;
                    }

                    referencedActions.Add(comboAction);
                }
            }
        }

        private static void ValidateCamera(
            IReadOnlyList<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            List<Camera> mainCameras = GetActiveComponents<Camera>(sceneObjects)
                .Where(camera => camera.enabled && camera.CompareTag("MainCamera"))
                .ToList();

            if (mainCameras.Count == 0)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "CAMERA_MAIN_MISSING",
                    "镜头",
                    "场景中没有标记为 MainCamera 的 Camera。",
                    null);
            }
            else if (mainCameras.Count > 1)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "CAMERA_MAIN_MULTIPLE",
                    "镜头",
                    $"场景中存在 {mainCameras.Count} 个 MainCamera，Camera.main 与监听器可能指向不确定。",
                    mainCameras[0]);
            }

            foreach (Camera camera in mainCameras)
            {
                bool hasCinemachineBrain = camera.GetComponents<Component>()
                    .Any(component => component != null
                        && component.GetType().Name == "CinemachineBrain");
                if (!hasCinemachineBrain)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "CAMERA_BRAIN_MISSING",
                        "镜头",
                        "MainCamera 未发现 CinemachineBrain。",
                        camera);
                }
            }

            List<PlayerViewController> viewControllers =
                GetActiveComponents<PlayerViewController>(sceneObjects)
                    .Where(controller => controller.enabled)
                    .ToList();
            if (viewControllers.Count == 0)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "CAMERA_VIEW_CONTROLLER_MISSING",
                    "镜头",
                    "场景中没有 PlayerViewController，Fast Action / Souls Like 两套视角预设不会生效。",
                    null);
            }
            else if (viewControllers.Count > 1)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "CAMERA_VIEW_CONTROLLER_MULTIPLE",
                    "镜头",
                    $"场景中存在 {viewControllers.Count} 个 PlayerViewController，请确认只有一个视角模式入口。",
                    viewControllers[0]);
            }
        }

        private static void ValidateCombat(
            IReadOnlyList<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            List<CombatFeedbackDispatcher> dispatchers =
                GetActiveComponents<CombatFeedbackDispatcher>(sceneObjects)
                    .Where(dispatcher => dispatcher.enabled)
                    .ToList();
            if (dispatchers.Count == 0)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "COMBAT_FEEDBACK_MISSING",
                    "战斗",
                    "场景中没有 CombatFeedbackDispatcher，Hitstop、镜头冲击、震动、音效与 VFX 请求不会被执行。",
                    null);
            }
            else if (dispatchers.Count > 1)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "COMBAT_FEEDBACK_MULTIPLE",
                    "战斗",
                    $"场景中存在 {dispatchers.Count} 个 CombatFeedbackDispatcher，反馈可能被重复执行。",
                    dispatchers[0]);
            }
        }

        private static void ValidateEnemies(
            IReadOnlyList<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues,
            ISet<CombatActionDefinition> referencedActions)
        {
            List<EnemyActor> enemies = GetActiveComponents<EnemyActor>(sceneObjects);
            if (enemies.Count == 0)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Info,
                    "ENEMY_NONE",
                    "敌人",
                    "活动场景中没有正式 EnemyActor。",
                    null);
                return;
            }

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            foreach (EnemyActor enemy in enemies)
            {
                GameObject root = enemy.gameObject;
                Require<EnemyHealth>(root, issues, "ENEMY_HEALTH_MISSING", "敌人缺少 EnemyHealth。", true);
                Require<EnemyBalance>(root, issues, "ENEMY_BALANCE_MISSING", "敌人缺少 EnemyBalance。", true);
                Require<CombatTagContainer>(root, issues, "ENEMY_TAGS_MISSING", "敌人缺少 CombatTagContainer。", true);
                Require<CombatTarget>(root, issues, "ENEMY_TARGET_MISSING", "敌人缺少 CombatTarget。", true);
                Require<EnemyStateMachine>(root, issues, "ENEMY_STATE_MISSING", "敌人缺少 EnemyStateMachine。", false);
                Require<EnemyTargetSensor>(root, issues, "ENEMY_SENSOR_MISSING", "敌人缺少 EnemyTargetSensor，无法自动发现玩家。", false);

                EnemyCombatDriver combatDriver = root.GetComponent<EnemyCombatDriver>();
                if (combatDriver == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "ENEMY_COMBAT_DRIVER_MISSING",
                        "敌人",
                        "敌人缺少 EnemyCombatDriver；仅当它是非攻击单位时可忽略。",
                        root);
                }
                else
                {
                    if (combatDriver.BasicAttackAction == null)
                    {
                        Add(
                            issues,
                            SceneValidationSeverity.Error,
                            "ENEMY_BASIC_ACTION_MISSING",
                            "战斗",
                            "EnemyCombatDriver 未配置基础攻击动作。",
                            combatDriver);
                    }
                    else
                    {
                        referencedActions.Add(combatDriver.BasicAttackAction);
                    }

                    if (combatDriver.SkillAction != null)
                    {
                        referencedActions.Add(combatDriver.SkillAction);
                    }
                }

                bool isGroundRole = enemy.CombatRole == EnemyCombatRole.GroundMelee
                    || enemy.CombatRole == EnemyCombatRole.GroundRanged;
                EnemyMotorBase motor = root.GetComponent<EnemyMotorBase>();
                if (isGroundRole && motor == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "ENEMY_GROUND_MOTOR_MISSING",
                        "敌人",
                        "地面敌人缺少 EnemyMotorBase。",
                        root);
                }
                else if (isGroundRole && enemy.Motor == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "ENEMY_MOTOR_REFERENCE_MISSING",
                        "敌人",
                        "EnemyActor 的 Motor 引用未指向已挂载的 EnemyMotorBase。",
                        enemy);
                }

                if (combatDriver != null && enemy.CombatDriver == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "ENEMY_DRIVER_REFERENCE_MISSING",
                        "敌人",
                        "EnemyActor 的 Combat Driver 引用未指向已挂载的 EnemyCombatDriver。",
                        enemy);
                }

                Animator animator = root.GetComponentInChildren<Animator>(true);
                EnemyAnimatorDriver animatorDriver = root.GetComponent<EnemyAnimatorDriver>();
                if (animator != null && animatorDriver == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "ENEMY_ANIMATOR_DRIVER_MISSING",
                        "敌人",
                        "敌人已有 Animator，但根物体缺少 EnemyAnimatorDriver。",
                        root);
                }

                if (enemy.BodyRoot == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "ENEMY_BODY_ROOT_MISSING",
                        "敌人",
                        "EnemyActor 的 Body Root 未配置，动画和表现层无法稳定定位视觉根。",
                        enemy);
                }

                if (enemyLayer >= 0 && root.layer != enemyLayer)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "ENEMY_LAYER_INVALID",
                        "Layer",
                        $"敌人根物体应位于 Enemy Layer，当前为 {LayerMask.LayerToName(root.layer)}。",
                        root);
                }

                if (enemyLayer >= 0)
                {
                    int invalidColliderCount = root
                        .GetComponentsInChildren<Collider>(true)
                        .Count(collider => collider.gameObject.layer != enemyLayer);
                    if (invalidColliderCount > 0)
                    {
                        Add(
                            issues,
                            SceneValidationSeverity.Warning,
                            "ENEMY_COLLIDER_LAYER_MIXED",
                            "Layer",
                            $"敌人层级中有 {invalidColliderCount} 个 Collider 不在 Enemy Layer，Hitbox LayerMask 可能无法命中。",
                            root);
                    }
                }
            }
        }

        private static void CollectActionReferences(
            IEnumerable<GameObject> sceneObjects,
            ISet<CombatActionDefinition> actions)
        {
            foreach (MonoBehaviour behaviour in sceneObjects
                         .SelectMany(sceneObject => sceneObject.GetComponents<MonoBehaviour>())
                         .Where(behaviour => behaviour != null))
            {
                try
                {
                    SerializedObject serializedObject = new(behaviour);
                    SerializedProperty property = serializedObject.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = true;
                        if (property.propertyType == SerializedPropertyType.ObjectReference
                            && property.objectReferenceValue is CombatActionDefinition action)
                        {
                            actions.Add(action);
                        }
                    }
                }
                catch
                {
                    // 引用检查阶段已经报告无法序列化的组件，这里只负责收集可读取的 Action。
                }
            }
        }

        private static void ValidateActions(
            IEnumerable<CombatActionDefinition> actions,
            ICollection<SceneValidationIssue> issues)
        {
            List<CombatActionDefinition> actionList = actions
                .Where(action => action != null)
                .ToList();

            foreach (CombatActionDefinition action in actionList)
            {
                if (string.IsNullOrWhiteSpace(action.ActionId))
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "ACTION_ID_MISSING",
                        "战斗数据",
                        "场景引用的 CombatActionDefinition 没有 Action Id。",
                        action);
                }

                if (action.ActionType != CombatActionType.Ultimate && action.HitboxPrefab == null)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Error,
                        "ACTION_HITBOX_MISSING",
                        "战斗数据",
                        $"动作 {action.name} 未配置 Hitbox Prefab，当前执行器无法执行该动作。",
                        action);
                }
            }

            foreach (IGrouping<string, CombatActionDefinition> group in actionList
                         .Where(action => !string.IsNullOrWhiteSpace(action.ActionId))
                         .GroupBy(action => action.ActionId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "ACTION_ID_DUPLICATE",
                    "战斗数据",
                    $"场景引用了 {group.Count()} 个相同 Action Id 的资产：{group.Key}。动画与调试路由可能无法区分。",
                    group.First());
            }
        }

        private static void ValidateNavMesh(
            IReadOnlyList<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            List<NavMeshAgent> agents = GetActiveComponents<NavMeshAgent>(sceneObjects)
                .Where(agent => agent.enabled)
                .ToList();
            if (agents.Count == 0)
            {
                return;
            }

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "NAVMESH_DATA_MISSING",
                    "NavMesh",
                    $"场景中存在 {agents.Count} 个 NavMeshAgent，但当前没有可用的 NavMesh 数据。",
                    agents[0]);
                return;
            }

            foreach (NavMeshAgent agent in agents)
            {
                if (!agent.enabled || !agent.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bool isOnNavMesh;
                try
                {
                    isOnNavMesh = agent.isOnNavMesh;
                }
                catch
                {
                    isOnNavMesh = false;
                }

                if (!isOnNavMesh)
                {
                    Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "NAVMESH_AGENT_OFF_MESH",
                        "NavMesh",
                        "启用的 NavMeshAgent 当前不在已烘焙 NavMesh 上。",
                        agent);
                }
            }
        }

        private static void Require<T>(
            GameObject owner,
            ICollection<SceneValidationIssue> issues,
            string code,
            string message,
            bool isError)
            where T : Component
        {
            if (owner.GetComponent<T>() != null)
            {
                return;
            }

            Add(
                issues,
                isError ? SceneValidationSeverity.Error : SceneValidationSeverity.Warning,
                code,
                owner.GetComponent<EnemyActor>() != null ? "敌人" : "玩家",
                message,
                owner);
        }

        private static List<T> GetComponents<T>(IEnumerable<GameObject> sceneObjects)
            where T : Component
        {
            return sceneObjects
                .Select(sceneObject => sceneObject.GetComponent<T>())
                .Where(component => component != null)
                .ToList();
        }

        private static List<T> GetActiveComponents<T>(IEnumerable<GameObject> sceneObjects)
            where T : Component
        {
            return GetComponents<T>(sceneObjects)
                .Where(component => component.gameObject.activeInHierarchy)
                .ToList();
        }

        private static void Add(
            ICollection<SceneValidationIssue> issues,
            SceneValidationSeverity severity,
            string code,
            string category,
            string message,
            Object context)
        {
            issues.Add(new SceneValidationIssue(
                severity,
                code,
                category,
                message,
                context,
                GetContextPath(context)));
        }

        private static string GetContextPath(Object context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            string assetPath = AssetDatabase.GetAssetPath(context);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                return assetPath;
            }

            Transform transform = context switch
            {
                GameObject gameObject => gameObject.transform,
                Component component => component.transform,
                _ => null
            };

            if (transform == null)
            {
                return context.name;
            }

            Stack<string> names = new();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return $"{transform.gameObject.scene.name}/{string.Join("/", names)}";
        }

        private static int CompareIssues(
            SceneValidationIssue left,
            SceneValidationIssue right)
        {
            int severityComparison = right.Severity.CompareTo(left.Severity);
            if (severityComparison != 0)
            {
                return severityComparison;
            }

            int categoryComparison = string.Compare(
                left.Category,
                right.Category,
                StringComparison.Ordinal);
            if (categoryComparison != 0)
            {
                return categoryComparison;
            }

            return string.Compare(left.ContextPath, right.ContextPath, StringComparison.Ordinal);
        }
    }
}
