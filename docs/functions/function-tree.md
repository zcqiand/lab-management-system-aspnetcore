# 功能清单（Function Tree）— 建筑工程实验室管理系统ASP.NET-Core后端

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
| M06 | 检测能力 | 检测专项/项目/参数/标准/计算方法/技术要求/报告名称/参数界面 | 规划 |

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
| M01.F05.I02 | SSO 跳转 | 接口 | GET /api/auth/sso/authorize：HS256 签 state 写 HttpOnly Secure Cookie lab_sso_state，再 forward saas POST /api/v1/oauth/authorize 拿 code；no-sso profile 走 NoopSaasAuthClient | 已上线 |
| M01.F05.I03 | SSO 回调 | 接口 | POST /api/auth/sso/callback:校验 body.state==cookie nonce + cookie 签名,再 saas POST /api/v1/oauth/token 换 token,再 /me/whoami + /me/tenants 拿 user,membership 信 saas;首次 SSO 按 email upsert 到 lab directory | 已上线 |
| M01.F05.I04 | 刷新 token | 接口 | POST /api/auth/refresh:lab refresh token 是 HS256 JWT(typ=refresh),内嵌 saas refresh token;调 saas POST /api/v1/oauth/token grantType=refresh_token 续,再签新 lab JWT | 已上线 |
| M01.F05.I05 | 登出 | 接口 | POST /api/auth/logout：无状态 JWT 服务端无 session，前端清存储 | 已上线 |

## M02 资源管理

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M02.F01 | 合同管理 | 合同 CRUD、工程信息维护 | 接口 | 已上线 |

### M02.F01 合同管理

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M02.F01.I01 | 合同列表 | 接口 | GET /api/contracts?keyword=&status=：keyword 模糊 contractCode/projectName 不敏、status 精确、tenant 收口 | 已上线 |
| M02.F01.I02 | 合同详情 | 接口 | GET /api/contracts/{id}，miss 404 | 已上线 |
| M02.F01.I03 | 创建合同 | 接口 | POST /api/contracts：id=C-UUID、status 默认 active | 已上线 |
| M02.F01.I04 | 更新合同 | 接口 | PUT /api/contracts/{id}：PATCH 语义 | 已上线 |
| M02.F01.I05 | 删除合同 | 接口 | DELETE /api/contracts/{id}：被接样引用 FK RESTRICT 拒 | 已上线 |

## M03 试验过程管理

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M03.F01 | 接样管理 | 接样单 CRUD、报告类别关联、流程状态 | 接口 | 已上线 |
| M03.F02 | 任务分配 | 接样提交后安排检测人员/计划日期，提交进入数据录入；任务字段挂 SampleReceipt | 接口 | 已上线 |
| M03.F03 | 数据录入 | 样品检测数据录入 | 接口 | 已上线 |
| M03.F05 | 报告审核 | 报告审核流程 | 接口 | 已上线 |
| M03.F06 | 报告批准 | 报告批准流程 | 接口 | 已上线 |
| M03.F07 | 报告发放 | 报告发放流程 | 接口 | 已上线 |
| M03.F08 | 报告归档 | 报告归档流程 | 接口 | 已上线 |
| M03.F09 | 接样单详情 | 接样单查看（接样信息+样品信息+检测数据） | 接口 | 已上线 |

### M03.F01 接样管理

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F01.I01 | 接样列表 | 接口 | GET /api/receipts?contractId=&flowStatus=&keyword=：keyword 模糊 commissionCode/projectName | 已上线 |
| M03.F01.I02 | 接样详情 | 接口 | GET /api/receipts/{id}，miss 404 | 已上线 |
| M03.F01.I03 | 创建接样 | 接口 | POST /api/receipts：contract FK 必存在、flow_status=receiving 起步、flow_history=[] | 已上线 |
| M03.F01.I04 | 更新接样 | 接口 | PUT /api/receipts/{id}：PATCH 语义（含 3 个 jsonb 列表） | 已上线 |
| M03.F01.I05 | 删除接样 | 接口 | DELETE /api/receipts/{id}：CASCADE 下属 samples | 已上线 |
| M03.F01.I06 | 流程历史 | 接口 | GET /api/receipts/{id}/history → FlowHistoryEntry[] | 已上线 |

### M03.F02 任务分配

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F02.I01 | 任务分配 | 接口 | PUT /api/receipts/{id}/task：assignee/plannedDate 挂 SampleReceipt；receiving 态自动推进 task_assignment 并写 history | 已上线 |

### M03.F03 数据录入

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F03.I01 | 样品列表 | 接口 | GET /api/samples?receiptId=&keyword=：keyword 模糊 sampleCode/sampleName，createdAt DESC | 已上线 |
| M03.F03.I02 | 样品详情 | 接口 | GET /api/samples/{id}，miss 404 | 已上线 |
| M03.F03.I03 | 创建样品 | 接口 | POST /api/samples：receipt FK 必存在、ext 默认 {} | 已上线 |
| M03.F03.I04 | 更新样品 | 接口 | PUT /api/samples/{id}：PATCH 语义 | 已上线 |
| M03.F03.I05 | 删除样品 | 接口 | DELETE /api/samples/{id} | 已上线 |
| M03.F03.I06 | 检测记录列表 | 接口 | GET /api/test-records?sampleId=：tenant+sampleId 过滤，分页回显 | 已上线 |
| M03.F03.I07 | 检测记录详情 | 接口 | GET /api/test-records/{id}，miss 404 | 已上线 |
| M03.F03.I08 | 创建检测记录 | 接口 | POST /api/test-records：sampleId/parameterCode/requirement/result 必填 | 已上线 |
| M03.F03.I09 | 更新检测记录 | 接口 | PUT /api/test-records/{id}：PATCH 语义 | 已上线 |
| M03.F03.I10 | 删除检测记录 | 接口 | DELETE /api/test-records/{id} | 已上线 |
| M03.F03.I11 | 改判 | 接口 | PATCH /api/test-records/{id}/verdict：verdict 直接覆写（生成契约 PATCH；springboot 侧 PUT） | 已上线 |

### M03.F05 报告审核

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F05.I01 | 审核队列 | 接口 | GET /api/receipts/flow/queue?stage=：stage 精确 + tenant 收口，pageSize 默认 50 cap 200 | 已上线 |
| M03.F05.I02 | 审核查看 | 接口 | GET /api/receipts/{id} review 视角（复用详情端点） | 已上线 |
| M03.F05.I03 | 通过退回 | 接口 | POST /api/receipts/flow：review 下 SUBMIT→approval / RETURN→data_entry | 已上线 |

### M03.F06 报告批准

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F06.I01 | 阶段推进 | 接口 | POST /api/receipts/flow 批量：SUBMIT/RETURN/WITHDRAW，单条失败进 FlowActionResult 不炸整批 | 已上线 |
| M03.F06.I02 | 批准查看 | 接口 | GET /api/receipts/{id} approval 视角 | 已上线 |
| M03.F06.I03 | 批准退回 | 接口 | POST /api/receipts/flow：approval 下 SUBMIT→issuance / RETURN→review | 已上线 |

### M03.F07 报告发放

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F07.I01 | 发放队列 | 接口 | GET /api/receipts/flow/queue?stage=issuance | 已上线 |
| M03.F07.I02 | 发放查看 | 接口 | GET /api/receipts/{id} issuance 视角（含 issued_at） | 已上线 |
| M03.F07.I03 | 发放退回 | 接口 | POST /api/receipts/flow：issuance 下 SUBMIT→archived / RETURN→approval | 已上线 |

### M03.F08 报告归档

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F08.I01 | 归档队列 | 接口 | GET /api/receipts/flow/queue?stage=archived | 已上线 |
| M03.F08.I02 | 归档查看 | 接口 | GET /api/receipts/{id} archived 视角 | 已上线 |
| M03.F08.I03 | 归档退回 | 接口 | POST /api/receipts/flow：archived 下 SUBMIT 无效（终态）/ RETURN→issuance；WITHDRAW 仅 receiving 自转移 | 已上线 |

### M03.F09 接样单详情

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M03.F09.I01 | 接样单详情聚合 | 接口 | GET /api/receipts/{id} + 客户端组合 samples/test-records 三视图 | 已上线 |

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
| M05.F01 | 报告汇总 | 按报告类别输出试验报告汇总表 | 查询 | 已上线 |
| M05.F02 | 仪表盘统计 | 工作台仪表盘：合同/接样/样品计数 + 按 3 桶聚合的报告状态 + 任务计数 | 查询 | 已上线 |

### M05.F01 报告汇总

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M05.F01.I01 | 报告汇总 | 接口 | GET /api/summary?categoryCode=&dateFrom=&dateTo=：ALL 不过滤按报告类别过滤当前租户接样单；SummaryData{summaryName, 6 列, rows}；commissionDate DESC, commissionCode 排序 | 已上线 |
| M05.F01.I03 | 核心指标卡 | 接口 | GET /api/summary/stats 扩展：todayTestCount（今日试验总数）+ qualifiedRateByMaterial{concrete,rebar,sand}（按材料合格率，码表 summaryName 关键词映射，全量预载防 N+1）+ reportOutputByStatus{generated,pending,issued}（报告产出量） | 已上线 |
| M05.F01.I04 | 任务状态漏斗 | 接口 | GET /api/summary/stats 扩展：funnelByStage{pending_collect,received,testing,reporting,reviewing,issued} 六段实时计数 | 已上线 |

### M05.F02 仪表盘统计

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M05.F02.I01 | 仪表盘统计 | 接口 | GET /api/summary/stats：合同/接样/样品计数 + 3 桶报告状态（draft=receiving+task+data_entry；reviewing=review+approval；issued=issuance+archived）+ pendingTaskCount + I03/I04 扩展字段 | 已上线 |

## M06 检测能力

| 功能 ID | 功能名称 | 闭环定义 | 类型 | 状态 |
|---|---|---|---|---|
| M06.F01 | 检测专项 | InspectionSpecialty CRUD（检测能力字典根） | 接口 | 已上线 |
| M06.F02 | 检测项目 | InspectionObject CRUD + 专项/参数关联 | 接口 | 已上线 |
| M06.F03 | 检测参数 | InspectionParameter CRUD + 标准/参数关联 | 接口 | 已上线 |
| M06.F04 | 检测标准 | InspectionStandard CRUD（含状态：active/superseded/draft） | 接口 | 已上线 |
| M06.F05 | 计算方法 | CalculationRule 维护（复合主键，算法类型 + 公式） | 接口 | 已上线 |
| M06.F06 | 技术要求 | TechnicalRequirement 维护，按四维度匹配；brand/model/grade/spec 改为 FK 引用实体 | 接口 | 已上线 |

### M06.F05 计算方法

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

### M06.F07 报告名称

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F07.I01 | 报告名称列表 | 接口 | GET /api/report-names?keyword= | 已上线 |
| M06.F07.I02 | 报告名称详情 | 接口 | GET /{code}：含 extFields List<ExtFieldDef> | 已上线 |
| M06.F07.I03 | 报告名称新增 | 接口 | POST：extFields 默认 [] | 已上线 |
| M06.F07.I04 | 报告名称修改 | 接口 | PUT PATCH | 已上线 |
| M06.F07.I05 | 报告名称删除 | 接口 | DELETE，miss 404 | 已上线 |
| M06.F07.I06 | 报告对象关联 | 接口 | POST /api/report-names/links/object：upsert | 已上线 |
| M06.F07.I07 | 报告标准关联 | 接口 | POST /api/report-names/links/standard（role 在 PK） | 已上线 |
| M06.F07.I08 | 报告参数关联 | 接口 | POST /api/report-names/links/parameter：upsert | 已上线 |

### M06.F08 参数界面

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F08.I01 | 参数界面列表 | 接口 | GET /api/param-interfaces?keyword= | 已上线 |
| M06.F08.I02 | 参数界面详情 | 接口 | GET /{code}：含 config Map | 已上线 |
| M06.F08.I03 | 参数界面新增 | 接口 | POST：code/componentPath 必填，config 默认 {} | 已上线 |
| M06.F08.I04 | 参数界面修改 | 接口 | PUT PATCH | 已上线 |
| M06.F08.I05 | 参数界面删除 | 接口 | DELETE，miss 404 | 已上线 |
| M06.F08.I06 | 参数界面关联 | 接口 | POST /api/param-interfaces/links：行级 config jsonb（区别于 PI.config） | 已上线 |
| M06.F07 | 报告名称 | InspectionReportName CRUD + extFields 模板 + 关联标准/参数 | 接口 | 已上线 |
| M06.F08 | 参数界面 | ParamInterface 维护 + 参数↔界面 link | 接口 | 已上线 |

### M06.F01 检测专项

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F01.I01 | 专项列表 | 接口 | GET /api/inspection/specialties?keyword=：keyword 模糊 code/name | 已上线 |
| M06.F01.I02 | 专项详情 | 接口 | GET /api/inspection/specialties/{code}，miss 404 | 已上线 |
| M06.F01.I03 | 专项新增 | 接口 | POST：code/officialNo/name 必填 | 已上线 |
| M06.F01.I04 | 专项修改 | 接口 | PUT PATCH 语义 | 已上线 |
| M06.F01.I05 | 专项标准关联 | 接口 | POST /api/inspection/links/object-standard：role 在 PK（同 code 对不同 role 两行） | 已上线 |
| M06.F01.I06 | 专项标准解除 | 接口 | DELETE unlink，miss 404 | 已上线 |

### M06.F02 检测项目

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F02.I01 | 项目列表 | 接口 | GET /api/inspection/objects?inspectionSpecialtyCode=&keyword= | 已上线 |
| M06.F02.I02 | 项目详情 | 接口 | GET /api/inspection/objects/{code}，miss 404 | 已上线 |
| M06.F02.I03 | 项目新增 | 接口 | POST：specialty FK RESTRICT、sourceProjectNo/Name 必填 | 已上线 |
| M06.F02.I04 | 项目修改 | 接口 | PUT PATCH 语义 | 已上线 |
| M06.F02.I05 | 专项项目关联 | 接口 | POST /api/inspection/links/specialty-object：upsert 幂等 | 已上线 |
| M06.F02.I06 | 专项项目解除 | 接口 | DELETE unlink，miss 404 | 已上线 |
| M06.F02.I07 | 项目参数关联 | 接口 | POST /api/inspection/links/object-parameter：qualificationLevel 默认 QUALIFIED | 已上线 |
| M06.F02.I08 | 项目参数解除 | 接口 | DELETE unlink，miss 404 | 已上线 |

### M06.F03 检测参数

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F03.I01 | 参数列表 | 接口 | GET /api/inspection/parameters?keyword=&sourceType= | 已上线 |
| M06.F03.I02 | 参数详情 | 接口 | GET /{code}，miss 404 | 已上线 |
| M06.F03.I03 | 参数新增 | 接口 | POST：aliases 默认 []、sourceType 默认 OFFICIAL | 已上线 |
| M06.F03.I04 | 参数修改 | 接口 | PUT PATCH；aliases 传则整体替换 | 已上线 |
| M06.F03.I05 | 标准参数关联 | 接口 | POST /api/inspection/links/standard-parameter：upsert | 已上线 |
| M06.F03.I06 | 标准参数解除 | 接口 | DELETE unlink，miss 404 | 已上线 |
| M06.F03.I07 | 参数界面解除 | 接口 | DELETE /api/param-interfaces/links unlink（参数侧视角） | 已上线 |

### M06.F04 检测标准

| 子项 ID | 名称 | 类型 | 说明 | 状态 |
|---|---|---|---|---|
| M06.F04.I01 | 标准列表 | 接口 | GET /api/inspection/standards?keyword=&status= | 已上线 |
| M06.F04.I02 | 标准详情 | 接口 | GET /{code}，miss 404 | 已上线 |
| M06.F04.I03 | 标准新增 | 接口 | POST：status 默认 ACTIVE | 已上线 |
| M06.F04.I04 | 标准修改 | 接口 | PUT PATCH（含 active/superseded/draft 状态迁移） | 已上线 |
| M06.F04.I05 | 报告对象解除 | 接口 | DELETE /api/report-names/links/object unlink（标准侧视角） | 已上线 |
| M06.F04.I06 | 报告参数解除 | 接口 | DELETE /api/report-names/links/parameter unlink（标准侧视角） | 已上线 |
| M06.F04.I07 | 报告标准解除 | 接口 | DELETE /api/report-names/links/standard unlink（role 在 PK） | 已上线 |

---

## 维护约定

- 谁改功能，谁改表，同一个 commit。
- `规划` → `开发中`：必须先有需求文档引用它。
- `开发中` → `已上线`：L5 会警告它缺设计映射与测试引用。警告不阻断，由人裁量。
