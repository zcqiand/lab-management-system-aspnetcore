# 设计与功能对齐 — 实验室管理系统ASP.NET-Core后端

> 人填、人评审。机器只检查功能 ID 存在性。
> 回答一个问题：**这个功能子项，落到哪段代码、哪张表、哪个权限码上？**
> 答不上来的行，说明设计没做完，别开工。

## 映射表

| 功能子项 ID | 页面/组件 | 接口 | 数据表 | 权限码 | 设计稿 | 状态 |
|---|---|---|---|---|---|---|
| M00.F01.I01 | AuthController#GetCurrentCurrentUser / AuthService#Me | GET /api/auth/me | -（配置式目录） | M00.F01.I01 | - | 已上线 |
| M00.F02.I01 | AuthController#SwitchTenant / AuthService#SwitchTenant | POST /api/auth/switch-tenant | -（配置式目录） | M00.F02.I01 | - | 已上线 |
| M01.F04.I01 | AuthController#GetMenus / AuthService#Menus | GET /api/auth/menus | - | M01.F04.I01 | - | 已上线 |
| M01.F04.I02 | AuthController#GetPermissions / AuthService#Permissions | GET /api/auth/permissions | - | M01.F04.I02 | - | 已上线 |
| M01.F05.I01 | AuthController#Login / AuthService#Login | POST /api/auth/login | -（配置式目录） | M01.F05.I01 | - | 已上线 |
| M01.F05.I02 | AuthController#SsoAuthorize / AuthService#SsoAuthorize | GET /api/auth/sso/authorize | - | M01.F05.I02 | - | 已上线 |
| M01.F05.I03 | AuthController#SsoCallback / AuthService#SsoCallback | POST /api/auth/sso/callback | - | M01.F05.I03 | - | 已上线 |
| M01.F05.I04 | AuthController#Refresh / AuthService#Refresh | POST /api/auth/refresh | - | M01.F05.I04 | - | 已上线 |
| M01.F05.I05 | AuthController#Logout / AuthService#Logout | POST /api/auth/logout | - | M01.F05.I05 | - | 已上线 |
| M04.F06.I01 | CatalogController#ListModels / CatalogService#ListModels | GET /api/catalog/models | inspection_models（内存 store 镜像 V004 语义） | M04.F06.I01 | - | 已上线 |
| M04.F06.I02 | CatalogController#CreateModel / CatalogService#CreateModel | POST /api/catalog/models | inspection_models | M04.F06.I02 | - | 已上线 |
| M04.F06.I03 | CatalogController#UpdateModel / CatalogService#UpdateModel | PUT /api/catalog/models/{code} | inspection_models | M04.F06.I03 | - | 已上线 |
| M04.F06.I04 | CatalogController#DeleteModel / CatalogService#DeleteModel | DELETE /api/catalog/models/{code} | inspection_models | M04.F06.I04 | - | 已上线 |
| M04.F07.I01 | CatalogController#ListSpecs / CatalogService#ListSpecs | GET /api/catalog/specs | inspection_specs | M04.F07.I01 | - | 已上线 |
| M04.F07.I02 | CatalogController#CreateSpec / CatalogService#CreateSpec | POST /api/catalog/specs | inspection_specs | M04.F07.I02 | - | 已上线 |
| M04.F07.I03 | CatalogController#UpdateSpec / CatalogService#UpdateSpec | PUT /api/catalog/specs/{code} | inspection_specs | M04.F07.I03 | - | 已上线 |
| M04.F07.I04 | CatalogController#DeleteSpec / CatalogService#DeleteSpec | DELETE /api/catalog/specs/{code} | inspection_specs | M04.F07.I04 | - | 已上线 |
| M04.F08.I01 | CatalogController#ListGrades / CatalogService#ListGrades | GET /api/catalog/grades | inspection_grades | M04.F08.I01 | - | 已上线 |
| M04.F08.I02 | CatalogController#CreateGrade / CatalogService#CreateGrade | POST /api/catalog/grades | inspection_grades | M04.F08.I02 | - | 已上线 |
| M04.F08.I03 | CatalogController#UpdateGrade / CatalogService#UpdateGrade | PUT /api/catalog/grades/{code} | inspection_grades | M04.F08.I03 | - | 已上线 |
| M04.F08.I04 | CatalogController#DeleteGrade / CatalogService#DeleteGrade | DELETE /api/catalog/grades/{code} | inspection_grades | M04.F08.I04 | - | 已上线 |
| M04.F09.I01 | CatalogController#ListBrands / CatalogService#ListBrands | GET /api/catalog/brands | inspection_brands | M04.F09.I01 | - | 已上线 |
| M04.F09.I02 | CatalogController#CreateBrand / CatalogService#CreateBrand | POST /api/catalog/brands | inspection_brands | M04.F09.I02 | - | 已上线 |
| M04.F09.I03 | CatalogController#UpdateBrand / CatalogService#UpdateBrand | PUT /api/catalog/brands/{code} | inspection_brands | M04.F09.I03 | - | 已上线 |
| M04.F09.I04 | CatalogController#DeleteBrand / CatalogService#DeleteBrand | DELETE /api/catalog/brands/{code} | inspection_brands + technical_requirements.brand SET NULL | M04.F09.I04 | - | 已上线 |
| M06.F05.I01 | CalculationRulesController#ListCalculationRules / CalculationRuleService#List | GET /api/calculation-rules | inspection_calculation_rules（平台级） | M06.F05.I01 | - | 已上线 |
| M06.F05.I02 | CalculationRulesController#GetCalculationRule / CalculationRuleService#Get | GET /api/calculation-rules/{object}/{parameter} | inspection_calculation_rules | M06.F05.I02 | - | 已上线 |
| M06.F05.I03 | CalculationRulesController#CreateCalculationRule / CalculationRuleService#Create | POST /api/calculation-rules | inspection_calculation_rules | M06.F05.I03 | - | 已上线 |
| M06.F05.I04 | CalculationRulesController#UpdateCalculationRule / CalculationRuleService#Update | PUT /api/calculation-rules/{object}/{parameter} | inspection_calculation_rules | M06.F05.I04 | - | 已上线 |
| M06.F05.I05 | CalculationRulesController#DeleteCalculationRule / CalculationRuleService#Delete | DELETE /api/calculation-rules/{object}/{parameter} | inspection_calculation_rules | M06.F05.I05 | - | 已上线 |
| M06.F06.I01 | TechnicalRequirementsController#ListTechnicalRequirements / TechnicalRequirementService#List | GET /api/technical-requirements | inspection_technical_requirements | M06.F06.I01 | - | 已上线 |
| M06.F06.I02 | TechnicalRequirementsController#GetTechnicalRequirement / TechnicalRequirementService#Get | GET /api/technical-requirements/{object}/{parameter}/{standard} | inspection_technical_requirements | M06.F06.I02 | - | 已上线 |
| M06.F06.I03 | TechnicalRequirementsController#CreateTechnicalRequirement / TechnicalRequirementService#Create | POST /api/technical-requirements | inspection_technical_requirements | M06.F06.I03 | - | 已上线 |
| M06.F06.I04 | TechnicalRequirementsController#UpdateTechnicalRequirement / TechnicalRequirementService#Update | PUT /api/technical-requirements/{object}/{parameter}/{standard} | inspection_technical_requirements | M06.F06.I04 | - | 已上线 |
| M06.F06.I05 | TechnicalRequirementsController#DeleteTechnicalRequirement / TechnicalRequirementService#Delete | DELETE /api/technical-requirements/{object}/{parameter}/{standard} | inspection_technical_requirements | M06.F06.I05 | - | 已上线 |

## 约定

1. **权限码 = 功能子项 ID。** 前端按钮的权限判断直接写 ID。
2. 一个接口服务多个子项时，多行重复写。不要为表好看而合并 —— 合并后看不清接口还有没有别的调用方。
3. 状态列必须与功能清单一致。不一致以功能清单为准。

## 评审时问这三个问题

1. 有没有子项没有权限码？→ 那它就是任何人都能点的按钮
2. 有没有一张表被三个以上模块直接写入？→ 边界破了
3. 「开发中」的行里接口和表填了吗？→ 没填就是还在纸上，别报进度
