# 功能清单（Function Tree）— 实验室管理系统ASP.NET-Core后端

> **全体系唯一锚点。** 需求、流程、设计、测试都引用这里的 ID。
> 不在这里的 ID 是悬空引用，L5 门会拦。**改功能，先改这份表。**

## 编号规则

| 层级 | 名称 | 格式 | 含义 |
|---|---|---|---|
| 一级 | 功能模块 | `M01` | 业务域边界，通常对应一级菜单 |
| 二级 | 功能 | `M01.F01` | 一个完整业务步骤 / 独立闭环流程 / 数据管理页面 |
| 三级 | 功能子项 | `M01.F01.I01` | 最小操作单元。标签页、查询条件、增删改查/审核/导入导出按钮 |

**硬规则**

1. 编号单调递增，永不复用。废弃改状态，不删行。
2. 子项编号必须以父级为前缀。
3. 一个子项 = 一个权限点。权限码即 ID，不另起一套编码。
4. 拆不出子项的功能 → 它其实是子项，往上并。子项超 20 个 → 它其实是模块，往下拆。

**状态**：`规划` | `开发中` | `已上线` | `已废弃`
**子项类型**：`页面` | `标签页` | `查询` | `按钮` | `报表` | `接口`

> 编号镜像 lab-management-system-springboot 功能清单（含跳号，如 M03 无 F04、
> M04 从 F06 起），保证家族跨仓同一 ID 指同一功能。I 级子项不预拆，
> 等第一个需求落到对应模块时再拆。

---

## 模块总览

| 模块 ID | 模块名称 | 业务域边界 | 状态 |
|---|---|---|---|
| M00 | 租户管理 | 当前用户关联租户列表、登录选租户、切换租户 | 规划 |
| M01 | 认证管理 | 权限管理（RBAC/动态菜单）、认证（登录/SSO/JWT） | 规划 |
| M02 | 资源管理 | 合同管理 | 规划 |
| M03 | 试验过程管理 | 接样 → 任务分配 → 数据录入 → 报告审核 → 批准 → 发放 → 归档 | 规划 |
| M04 | 基础数据 | 型号/规格/等级/牌号维护 | 规划 |
| M05 | 数据统计 | 报告汇总表（按报告名称） | 规划 |
| M06 | 检测能力 | 检测专项/项目/参数/标准/计算规则/技术要求/报告名称/参数界面 | 规划 |

---

## M00 租户管理

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M00.F01 | 当前用户会话 | 当前用户信息 + 关联租户列表 + 当前选中租户（GET /auth/me） | 接口 | 已上线 |
| M00.F02 | 登录选租户 | 登录后选择租户，换发携带 tenant_id claim 的 token（POST /auth/switch-tenant） | 接口 | 已上线 |

### M00.F01 当前用户会话

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M00.F01.I01 | 当前会话 | 接口 | GET /api/auth/me：user + 关联租户列表 + currentTenantId（token tenant_id claim，缺省 TENANT-001） | 已上线 |

### M00.F02 登录选租户

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M00.F02.I01 | 选租户换发 | 接口 | POST /api/auth/switch-tenant：校验租户归属后换发携带 tenant_id claim 的 token | 已上线 |

## M01 认证管理

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M01.F04 | 权限管理 | RBAC 角色权限、路由守卫、权限指令、动态菜单（身份平台下发） | 接口 | 已上线 |
| M01.F05 | 认证管理 | 用户名+密码登录 + SSO 统一登录（对接身份平台），JWT 签发与校验 | 接口 | 已上线 |

### M01.F04 权限管理

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M01.F04.I01 | 动态菜单 | 接口 | GET /api/auth/menus：按角色下发导航树（5 根节点，镜像 lab-msw） | 已上线 |
| M01.F04.I02 | 权限集 | 接口 | GET /api/auth/permissions：RBAC 权限串列表（admin 全量 11 项） | 已上线 |

### M01.F05 认证管理

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M01.F05.I01 | 密码登录 | 接口 | POST /api/auth/login：用户名+密码校验，签发 access/refresh token + 租户列表 | 已上线 |
| M01.F05.I02 | SSO 跳转 | 接口 | GET /api/auth/sso/authorize?redirect=：构造 saas 身份平台登录跳转 URL + state | 已上线 |
| M01.F05.I03 | SSO 回调 | 接口 | POST /api/auth/sso/callback：dev 直发 demo 会话（真对接待 saas 端点可用） | 已上线 |
| M01.F05.I04 | 刷新 token | 接口 | POST /api/auth/refresh：refresh token 换发新 access token | 已上线 |
| M01.F05.I05 | 登出 | 接口 | POST /api/auth/logout：无状态 JWT 服务端无 session，前端清存储 | 已上线 |

## M02 资源管理

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M02.F01 | 合同管理 | 合同 CRUD、工程信息维护 | 接口 | 规划 |

## M03 试验过程管理

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M03.F01 | 接样管理 | 接样单 CRUD、报告类别关联、流程状态 | 接口 | 规划 |
| M03.F02 | 任务分配 | 接样提交后安排检测人员/计划日期，提交进入数据录入；任务字段挂 SampleReceipt | 接口 | 规划 |
| M03.F03 | 数据录入 | 样品检测数据录入 | 接口 | 规划 |
| M03.F05 | 报告审核 | 报告审核流程 | 接口 | 规划 |
| M03.F06 | 报告批准 | 报告批准流程 | 接口 | 规划 |
| M03.F07 | 报告发放 | 报告发放流程 | 接口 | 规划 |
| M03.F08 | 报告归档 | 报告归档流程 | 接口 | 规划 |
| M03.F09 | 接样单详情 | 接样单查看（接样信息+样品信息+检测数据） | 接口 | 规划 |

## M04 基础数据

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M04.F06 | 型号维护 | InspectionModel 官方数据码表维护，列表按检测专项过滤 | 接口 | 已上线 |
| M04.F07 | 规格维护 | InspectionSpec 官方数据码表维护，列表按检测专项过滤 | 接口 | 已上线 |
| M04.F08 | 等级维护 | InspectionGrade 官方数据码表维护，列表按检测专项过滤 | 接口 | 已上线 |
| M04.F09 | 牌号维护 | InspectionBrand 官方数据码表维护，列表按检测专项过滤 | 接口 | 已上线 |

### M04.F06 型号维护

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M04.F06.I01 | 型号列表 | 接口 | GET /api/catalog/models：tenant + inspectionObjectCode 精确 + keyword 大小写不敏包含 code/name，排序 sortOrder,code | 已上线 |
| M04.F06.I02 | 型号新增 | 接口 | POST /api/catalog/models | 已上线 |
| M04.F06.I03 | 型号修改 | 接口 | PUT /api/catalog/models/{code}：PATCH 语义，未传字段保留 | 已上线 |
| M04.F06.I04 | 型号删除 | 接口 | DELETE /api/catalog/models/{code}，miss 404 | 已上线 |

### M04.F07 规格维护

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M04.F07.I01 | 规格列表 | 接口 | GET /api/catalog/specs（过滤同型号） | 已上线 |
| M04.F07.I02 | 规格新增 | 接口 | POST /api/catalog/specs | 已上线 |
| M04.F07.I03 | 规格修改 | 接口 | PUT /api/catalog/specs/{code} | 已上线 |
| M04.F07.I04 | 规格删除 | 接口 | DELETE /api/catalog/specs/{code} | 已上线 |

### M04.F08 等级维护

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M04.F08.I01 | 等级列表 | 接口 | GET /api/catalog/grades | 已上线 |
| M04.F08.I02 | 等级新增 | 接口 | POST /api/catalog/grades | 已上线 |
| M04.F08.I03 | 等级修改 | 接口 | PUT /api/catalog/grades/{code} | 已上线 |
| M04.F08.I04 | 等级删除 | 接口 | DELETE /api/catalog/grades/{code} | 已上线 |

### M04.F09 牌号维护

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M04.F09.I01 | 牌号列表 | 接口 | GET /api/catalog/brands | 已上线 |
| M04.F09.I02 | 牌号新增 | 接口 | POST /api/catalog/brands | 已上线 |
| M04.F09.I03 | 牌号修改 | 接口 | PUT /api/catalog/brands/{code} | 已上线 |
| M04.F09.I04 | 牌号删除 | 接口 | DELETE /api/catalog/brands/{code}；被 technical_requirements 引用时 brand 列 SET NULL（V011 FK 语义） | 已上线 |

## M05 数据统计

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M05.F01 | 报告汇总 | 按报告类别输出试验报告汇总表 | 查询 | 规划 |
| M05.F02 | 仪表盘统计 | 工作台仪表盘：合同/接样/样品计数 + 按 3 桶聚合的报告状态 + 任务计数 | 查询 | 规划 |

## M06 检测能力

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M06.F01 | 检测专项 | InspectionSpecialty CRUD（检测能力字典根） | 接口 | 规划 |
| M06.F02 | 检测项目 | InspectionObject CRUD + 专项/参数关联 | 接口 | 规划 |
| M06.F03 | 检测参数 | InspectionParameter CRUD + 标准/参数关联 | 接口 | 规划 |
| M06.F04 | 检测标准 | InspectionStandard CRUD（含状态：active/superseded/draft） | 接口 | 规划 |
| M06.F05 | 计算规则 | CalculationRule 维护（复合主键，算法类型 + 公式） | 接口 | 已上线 |
| M06.F06 | 技术要求 | TechnicalRequirement 维护，按四维度匹配；brand/model/grade/spec 改为 FK 引用实体 | 接口 | 已上线 |

### M06.F05 计算规则

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F05.I01 | 规则列表 | 接口 | GET /api/calculation-rules：objectCode/parameterCode 双过滤（平台级无 tenant） | 已上线 |
| M06.F05.I02 | 规则详情 | 接口 | GET /api/calculation-rules/{object}/{parameter} 复合键 | 已上线 |
| M06.F05.I03 | 规则新增 | 接口 | POST：默认 algorithmType=manual、specimenCount=1 | 已上线 |
| M06.F05.I04 | 规则修改 | 接口 | PUT 复合键 PATCH 语义 | 已上线 |
| M06.F05.I05 | 规则删除 | 接口 | DELETE 复合键，miss 404 | 已上线 |

### M06.F06 技术要求

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F06.I01 | 要求列表 | 接口 | GET /api/technical-requirements：object/parameter/standard + verificationStatus 四过滤 + tenant 隔离 | 已上线 |
| M06.F06.I02 | 要求详情 | 接口 | GET 三键 (object,parameter,judgmentStandard) | 已上线 |
| M06.F06.I03 | 要求新增 | 接口 | POST：默认 numeric/≥/manual/draft，tenant 从 token 注入 | 已上线 |
| M06.F06.I04 | 要求修改 | 接口 | PUT 三键 PATCH 语义（含 brand/model/grade/spec 四维度） | 已上线 |
| M06.F06.I05 | 要求删除 | 接口 | DELETE 三键，miss 404 | 已上线 |
| M06.F07 | 报告名称 | InspectionReportName CRUD + extFields 模板 + 关联标准/参数 | 接口 | 规划 |
| M06.F08 | 参数界面 | ParamInterface 维护 + 参数↔界面 link | 接口 | 规划 |

---

## 维护约定

- 谁改功能，谁改表，同一个 commit。
- `规划` → `开发中`：必须先有需求文档引用它。
- `开发中` → `已上线`：L5 会警告它缺设计映射与测试引用。警告不阻断，由人裁量。
