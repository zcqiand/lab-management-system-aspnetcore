#!/usr/bin/env python3
"""live smoke：起真实 HTTP 打 B1-B6 全域端点（镜像 springboot B7 的 ad-hoc 打点，留成可复跑脚本）。

用法：
  1. 起服务：dotnet run --project src/Lab.AspNetCore.csproj --urls http://127.0.0.1:8081
  2. 打点：python scripts/live_smoke.py [base-url]   # 默认 http://127.0.0.1:8081

约定：demo 目录 admin/dev123456，switch-tenant TENANT-001 换带 tenant_id 的 token。
字典域（B5/B6）无租户隔离，流程域（B3）从 token claim 取 tenant。
退出码 0 = 全 PASS；1 = 有 FAIL（明细见输出）。
"""
import json
import sys
import urllib.error
import urllib.request

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8081"

PASS = 0
FAIL = 0
FAILURES = []


def call(method, path, token=None, body=None, expect=200):
    """单次请求。expect=None 表示不检查状态码（返回原始信息给调用方判断）。"""
    global PASS, FAIL
    url = BASE + path
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as resp:
            status, payload = resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        status, payload = e.code, e.read().decode()
    parsed = None
    if payload:
        try:
            parsed = json.loads(payload)
        except json.JSONDecodeError:
            parsed = None
    return status, parsed


def check(name, method, path, token=None, body=None, expect=200, verify=None):
    """断言状态码（+可选 verify(parsed)）。"""
    global PASS, FAIL
    status, parsed = call(method, path, token, body)
    problems = []
    if expect is not None and status != expect:
        problems.append(f"status {status} != {expect}: {str(parsed)[:200]}")
    if not problems and verify:
        try:
            verify(parsed)
        except AssertionError as e:
            problems.append(f"verify: {e}")
    if problems:
        FAIL += 1
        FAILURES.append((name, problems))
        print(f"FAIL {name}: {'; '.join(problems)}")
    else:
        PASS += 1
        print(f"PASS {name}")


def main():
    # === B1 认证域（permitAll 三件 + 受保护四件） ===
    check("auth/login", "POST", "/api/auth/login",
          body={"username": "admin", "password": "dev123456"},
          verify=lambda r: assert_(r["token"], "token missing"))
    status, login = call("POST", "/api/auth/login", None,
                         {"username": "admin", "password": "dev123456"})
    plain_token = login["token"]

    check("auth/bad-password-401", "POST", "/api/auth/login",
          body={"username": "admin", "password": "wrong"}, expect=401)
    check("auth/unauth-me-401", "GET", "/api/auth/me", expect=401)

    check("auth/refresh", "POST", "/api/auth/refresh",
          body={"refreshToken": login["refreshToken"]},
          verify=lambda r: assert_(r["token"], "token missing"))

    status, switched = call("POST", "/api/auth/switch-tenant", plain_token,
                            {"tenantId": "TENANT-001"})
    tok = switched["token"] if switched else ""
    check("auth/switch-tenant", "POST", "/api/auth/switch-tenant",
          body={"tenantId": "TENANT-001"}, token=plain_token,
          verify=lambda r: assert_(r["token"], "token missing"))
    check("auth/me", "GET", "/api/auth/me", token=tok,
          verify=lambda r: assert_(r["user"]["username"] == "admin"
                                   and r["currentTenantId"] == "TENANT-001", "wrong session"))
    check("auth/menus", "GET", "/api/auth/menus", token=tok,
          verify=lambda r: assert_(isinstance(r, list) and r, "menus empty"))
    check("auth/permissions", "GET", "/api/auth/permissions", token=tok,
          verify=lambda r: assert_(isinstance(r, dict), "not an object"))
    check("auth/sso-authorize", "GET", "/api/auth/sso/authorize?redirect=/",
          token=None, verify=lambda r: assert_(r.get("authorizeUrl"), "no authorizeUrl"))

    # === B2 码表/规则/技术要求 ===
    check("catalog/create-brand", "POST", "/api/catalog/brands", token=tok,
          body={"code": "BR-SMOKE", "name": "smoke牌", "inspectionObjectCode": "OBJ-SMOKE"})
    # 注：net8 + NRT 下非空 query 参数是必填（缺了 400），空串 = 不过滤（service 语义）
    check("catalog/list-brands", "GET",
          "/api/catalog/brands?inspectionObjectCode=OBJ-SMOKE&keyword=smoke", token=tok,
          verify=lambda r: assert_(len(r) == 1, f"expect 1 got {len(r)}"))
    check("catalog/create-grade", "POST", "/api/catalog/grades", token=tok,
          body={"code": "GR-SMOKE", "name": "smoke等级"})
    check("rules/create", "POST", "/api/calculation-rules", token=tok,
          body={"inspectionObjectCode": "OBJ-SMOKE", "inspectionParameterCode": "P-SMOKE"},
          verify=lambda r: assert_(r["algorithmType"] == "manual", "default not manual"))
    check("rules/list", "GET",
          "/api/calculation-rules?inspectionObjectCode=OBJ-SMOKE&inspectionParameterCode=P-SMOKE",
          token=tok,
          verify=lambda r: assert_(len(r) == 1, "rule not listed"))
    check("requirements/create", "POST", "/api/technical-requirements", token=tok,
          body={"inspectionObjectCode": "OBJ-SMOKE", "inspectionParameterCode": "P-SMOKE",
                "judgmentStandardCode": "STD-SMOKE"},
          verify=lambda r: assert_(r["valueType"] == "numeric", "default not numeric"))
    check("requirements/list", "GET",
          "/api/technical-requirements?inspectionObjectCode=OBJ-SMOKE"
          "&inspectionParameterCode=P-SMOKE&judgmentStandardCode=STD-SMOKE", token=tok,
          verify=lambda r: assert_(len(r) >= 1, "requirement not listed"))

    # === B5 字典 5 实体（先建，给 B6 junction 当 FK） ===
    check("dict/create-specialty", "POST", "/api/inspection/specialties", token=tok,
          body={"code": "SP-SMOKE", "officialNo": "1", "name": "smoke专项"})
    check("dict/list-specialties", "GET", "/api/inspection/specialties?keyword=smoke", token=tok,
          verify=lambda r: assert_(len(r) == 1, "specialty not listed"))
    check("dict/create-parameter", "POST", "/api/inspection/parameters", token=tok,
          body={"code": "P-SMOKE", "name": "smoke参数", "rawName": "r", "canonicalName": "c"},
          verify=lambda r: assert_(r.get("aliases") == [], "aliases default not []"))
    check("dict/create-standard", "POST", "/api/inspection/standards", token=tok,
          body={"code": "STD-SMOKE", "name": "smoke标准"},
          verify=lambda r: assert_(r["status"] == "active", "status default not active"))
    check("dict/create-report-name", "POST", "/api/report-names", token=tok,
          body={"code": "RN-SMOKE", "name": "smoke报告"})
    check("dict/create-param-interface", "POST", "/api/param-interfaces", token=tok,
          body={"code": "PI-SMOKE", "componentPath": "/components/smoke"},
          verify=lambda r: assert_(r.get("config") == {}, "config default not {}"))

    # === B6 objects + 8 组 junction（link upsert / unlink 幂等语义抽查） ===
    check("objects/create", "POST", "/api/inspection/objects", token=tok,
          body={"code": "OBJ-SMOKE", "inspectionSpecialtyCode": "SP-SMOKE",
                "sourceProjectNo": "PRJ-SMOKE", "sourceProjectName": "smoke工程",
                "name": "smoke对象"})
    check("objects/list", "GET",
          "/api/inspection/objects?inspectionSpecialtyCode=SP-SMOKE&keyword=smoke", token=tok,
          verify=lambda r: assert_(len(r) == 1, "object not listed"))

    # specialty-object
    check("link/specialty-object", "POST", "/api/inspection/links/specialty-object", token=tok,
          body={"inspectionSpecialtyCode": "SP-SMOKE", "inspectionObjectCode": "OBJ-SMOKE"})
    check("link/specialty-object-upsert", "POST", "/api/inspection/links/specialty-object",
          token=tok,
          body={"inspectionSpecialtyCode": "SP-SMOKE", "inspectionObjectCode": "OBJ-SMOKE"})
    # object-parameter（qualificationLevel 默认 QUALIFIED）
    check("link/object-parameter", "POST", "/api/inspection/links/object-parameter", token=tok,
          body={"inspectionObjectCode": "OBJ-SMOKE", "inspectionParameterCode": "P-SMOKE"})
    # object-standard（role 在 PK，同对不同 role 两行 -> 只验 link+第二次不同 role 也成功）
    check("link/object-standard", "POST", "/api/inspection/links/object-standard", token=tok,
          body={"inspectionObjectCode": "OBJ-SMOKE", "inspectionStandardCode": "STD-SMOKE",
                "role": "TESTING"})
    check("link/object-standard-second-role", "POST", "/api/inspection/links/object-standard",
          token=tok,
          body={"inspectionObjectCode": "OBJ-SMOKE", "inspectionStandardCode": "STD-SMOKE",
                "role": "JUDGMENT"})
    # standard-parameter
    check("link/standard-parameter", "POST", "/api/inspection/links/standard-parameter",
          token=tok,
          body={"inspectionStandardCode": "STD-SMOKE", "inspectionParameterCode": "P-SMOKE"})
    # report-name 三个 link
    check("link/report-name-object", "POST", "/api/report-names/links/object", token=tok,
          body={"reportNameCode": "RN-SMOKE", "inspectionObjectCode": "OBJ-SMOKE"})
    check("link/report-name-standard", "POST", "/api/report-names/links/standard", token=tok,
          body={"reportNameCode": "RN-SMOKE", "inspectionStandardCode": "STD-SMOKE",
                "role": "TESTING"})
    check("link/report-name-parameter", "POST", "/api/report-names/links/parameter", token=tok,
          body={"reportNameCode": "RN-SMOKE", "inspectionParameterCode": "P-SMOKE"})
    # param-interface link（行级 config）
    check("link/param-interface", "POST", "/api/param-interfaces/links", token=tok,
          body={"inspectionParameterCode": "P-SMOKE", "paramInterfaceCode": "PI-SMOKE",
                "config": {"row": "level"}})
    # unlink：生成基类带 [FromBody]（DELETE 带 JSON body），幂等 miss 语义 404
    check("unlink/standard-parameter", "DELETE", "/api/inspection/links/standard-parameter",
          token=tok,
          body={"inspectionStandardCode": "STD-SMOKE", "inspectionParameterCode": "P-SMOKE"},
          expect=200)
    check("unlink/standard-parameter-miss-404", "DELETE",
          "/api/inspection/links/standard-parameter", token=tok,
          body={"inspectionStandardCode": "STD-SMOKE", "inspectionParameterCode": "P-SMOKE"},
          expect=404)

    # === B3 合同/接样/样品/流程/检测记录 ===
    status, contract = call("POST", "/api/contracts", tok, {
        "contractCode": "HT-SMOKE", "clientUnit": "甲方", "projectName": "smoke工程",
        "constructionUnit": "乙方", "witness": "见证人", "witnessUnit": "见证单位"})
    fatal_unless(contract and contract.get("id"), f"contracts/create 依赖失败: {status} {contract}")
    mark("contracts/create",
         status == 200 and contract.get("status") == "active" and contract.get("id"),
         [f"status {status}: {str(contract)[:200]}"])
    check("contracts/get", "GET", f"/api/contracts/{contract['id']}", token=tok,
          verify=lambda r: assert_(r["contractCode"] == "HT-SMOKE", "wrong contract"))
    check("contracts/list", "GET", "/api/contracts?keyword=HT-SMOKE", token=tok,
          verify=lambda r: assert_(len(r) >= 1, "contract not listed"))

    status, receipt = call("POST", "/api/receipts", tok, {
        "contractId": contract["id"], "commissionCode": "WT-SMOKE",
        "commissionDate": "2026-08-19", "categoryCode": "CAT-SMOKE",
        "projectName": "smoke工程", "receivedBy": "smoke员", "sampleSource": "送样",
        "testCategory": "常规", "witness": "w", "witnessUnit": "wu"})
    fatal_unless(receipt and receipt.get("id"), f"receipts/create 依赖失败: {status} {receipt}")
    mark("receipts/create", status == 200 and receipt.get("id"),
         [f"status {status}: {str(receipt)[:200]}"])
    check("receipts/list", "GET",
          f"/api/receipts?contractId={contract['id']}&keyword=WT-SMOKE", token=tok,
          verify=lambda r: assert_(len(r) >= 1, "receipt not listed"))

    status, sample = call("POST", "/api/samples", tok, {
        "receiptId": receipt["id"], "sampleCode": "S-SMOKE", "sampleName": "smoke试块"})
    fatal_unless(sample and sample.get("id"), f"samples/create 依赖失败: {status} {sample}")
    mark("samples/create", status == 200 and sample.get("id"),
         [f"status {status}: {str(sample)[:200]}"])

    # 流程：receiving --submit--> task_assignment --return--> receiving（前进一级即回退验证）
    check("flow/submit", "POST", "/api/receipts/flow", token=tok,
          body={"ids": [receipt["id"]], "action": "submit", "operator": "smoke员"},
          verify=lambda r: assert_(r[0]["ok"] is True
                                   and r[0]["flowStatus"] == "task_assignment",
                                   f"unexpected {r}"))
    check("flow/return", "POST", "/api/receipts/flow", token=tok,
          body={"ids": [receipt["id"]], "action": "return", "operator": "smoke员"},
          verify=lambda r: assert_(r[0]["ok"] is True and r[0]["flowStatus"] == "receiving",
                                   f"unexpected {r}"))
    check("flow/queue", "GET", "/api/receipts/flow/queue?stage=task_assignment", token=tok,
          verify=lambda r: assert_(isinstance(r.get("items"), list), f"not paged: {str(r)[:100]}"))

    status, record = call("POST", "/api/test-records", tok, {
        "sampleId": sample["id"], "parameterCode": "P-SMOKE",
        "requirement": "≥30MPa", "result": "35.2"})
    fatal_unless(record and record.get("id"), f"records/create 依赖失败: {status} {record}")
    mark("records/create", status == 200 and record.get("id"),
         [f"status {status}: {str(record)[:200]}"])
    check("records/list", "GET",
          f"/api/test-records?sampleId={sample['id']}&parameterCode=P-SMOKE", token=tok,
          verify=lambda r: assert_(len(r) >= 1, "record not listed"))
    check("records/verdict", "PATCH", f"/api/test-records/{record['id']}/verdict", token=tok,
          body={"verdict": "PASS"},
          verify=lambda r: assert_(r.get("verdict") == "PASS", "verdict not set"))

    # === B4 汇总/仪表盘 ===
    check("summary", "GET",
          "/api/summary?categoryCode=CAT-SMOKE&dateFrom=2026-01-01&dateTo=2026-12-31",
          token=tok,
          verify=lambda r: assert_(isinstance(r, dict), "not an object"))
    check("summary/stats", "GET", "/api/summary/stats", token=tok,
          verify=lambda r: assert_(isinstance(r, dict), "not an object"))

    # === B1 收尾：logout（body 契约要求 token 字段） ===
    check("auth/logout", "POST", "/api/auth/logout", token=plain_token,
          body={"token": login["refreshToken"]})

    print(f"\n=== {PASS} PASS / {FAIL} FAIL (base={BASE}) ===")
    if FAILURES:
        for name, probs in FAILURES:
            print(f"  {name}: {probs}")
        sys.exit(1)


def mark(name, ok, problems=None):
    """手工记一笔 PASS/FAIL（依赖链里已经 call 过、不想重打的场景）。"""
    global PASS, FAIL
    if ok:
        PASS += 1
        print(f"PASS {name}")
    else:
        FAIL += 1
        FAILURES.append((name, problems or ["unknown"]))
        print(f"FAIL {name}: {problems}")


def assert_(cond, msg):
    if not cond:
        raise AssertionError(msg)


def fatal_unless(cond, msg):
    if not cond:
        print(f"FATAL {msg}（后续依赖此步，中止）")
        sys.exit(1)


if __name__ == "__main__":
    main()
