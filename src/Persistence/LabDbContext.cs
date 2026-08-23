namespace Lab.AspNetCore.Persistence;

using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using Lab.AspNetCore.Controllers.Generated;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core 映射 lab_dev 共库（shared/sql/migrations V001-V014 累计态）。
/// EF 只镜像、不 Migrate（与 springboot Flyway baseline 冻结同一哲学，shared SQL 是 SSOT）。
///
/// 实体直接复用 NSwag 生成的 DTO 类（stores/services 的既有类型，换装零转换层）：
/// - 列名走 UseSnakeCaseNamingConvention（PascalCase -&gt; snake_case，与 SQL 全量一致，
///   逐列核对过 V001-V014；表名用 ToTable 显式给）
/// - 枚举列 V014 后全是 text，值 = 契约小写串（[EnumMember]），走 Wire 转换器 --
///   与线上 JSON 同一套值（net8 EF 默认 HasConversion&lt;string&gt; 用成员名，会分叉）
/// - jsonb 列：List&lt;string&gt; / List&lt;POCO&gt; / IDictionary（object 值需数据源
///   EnableDynamicJson，见 Program.cs）
/// - issuedAt 是 timestamptz 而 DTO 是 string：ISO 转换器（服务层从不写入，仅留位）
/// - AdditionalProperties（JsonExtensionData）逐实体 Ignore
/// </summary>
public class LabDbContext(DbContextOptions<LabDbContext> options) : DbContext(options)
{
    // B3 流程域
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<SampleReceipt> SampleReceipts => Set<SampleReceipt>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<TestRecord> TestRecords => Set<TestRecord>();

    // B2 码表 + 规则/技术要求
    public DbSet<InspectionBrand> InspectionBrands => Set<InspectionBrand>();
    public DbSet<InspectionModel> InspectionModels => Set<InspectionModel>();
    public DbSet<InspectionSpec> InspectionSpecs => Set<InspectionSpec>();
    public DbSet<InspectionGrade> InspectionGrades => Set<InspectionGrade>();
    public DbSet<CalculationMethod> CalculationMethods => Set<CalculationMethod>();
    public DbSet<TechnicalRequirement> TechnicalRequirements => Set<TechnicalRequirement>();

    // B5 字典 + B6 objects
    public DbSet<InspectionSpecialty> InspectionSpecialties => Set<InspectionSpecialty>();
    public DbSet<InspectionObject> InspectionObjects => Set<InspectionObject>();
    public DbSet<InspectionParameter> InspectionParameters => Set<InspectionParameter>();
    public DbSet<InspectionStandard> InspectionStandards => Set<InspectionStandard>();
    public DbSet<InspectionReportName> InspectionReportNames => Set<InspectionReportName>();
    public DbSet<ParamInterface> ParamInterfaces => Set<ParamInterface>();

    // B6 八组 junction
    public DbSet<SpecialtyObjectLink> SpecialtyObjectLinks => Set<SpecialtyObjectLink>();
    public DbSet<ObjectParameterLink> ObjectParameterLinks => Set<ObjectParameterLink>();
    public DbSet<ObjectStandardLink> ObjectStandardLinks => Set<ObjectStandardLink>();
    public DbSet<StandardParameterLink> StandardParameterLinks => Set<StandardParameterLink>();
    public DbSet<ObjectReportNameLink> ObjectReportNameLinks => Set<ObjectReportNameLink>();
    public DbSet<ReportNameStandardLink> ReportNameStandardLinks => Set<ReportNameStandardLink>();
    public DbSet<ReportNameParameterLink> ReportNameParameterLinks => Set<ReportNameParameterLink>();
    public DbSet<ParamInterfaceLink> ParamInterfaceLinks => Set<ParamInterfaceLink>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // === B3 流程域（tenant_id 列收口，PK=id；合同删除 RESTRICT / 接样删除 CASCADE 由 DB 承担） ===
        b.Entity<Contract>(e =>
        {
            e.ToTable("contracts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion(Wire<ContractStatus>());
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<SampleReceipt>(e =>
        {
            e.ToTable("sample_receipts");
            e.HasKey(x => x.Id);
            e.Property(x => x.JudgmentBasis).HasColumnType("jsonb");
            e.Property(x => x.TestingBasis).HasColumnType("jsonb");
            e.Property(x => x.TestParameters).HasColumnType("jsonb");
            e.Property(x => x.FlowHistory).HasColumnType("jsonb");
            e.Property(x => x.FlowStatus).HasConversion(Wire<FlowStatus>());
            e.Property(x => x.Result).HasConversion(Wire<ReceiptResult>());
            e.Property(x => x.IssuedAt).HasColumnType("timestamptz").HasConversion(IsoDateTime);
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<Sample>(e =>
        {
            e.ToTable("samples");
            e.HasKey(x => x.Id);
            e.Property(x => x.Ext).HasColumnType("jsonb");
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<TestRecord>(e =>
        {
            e.ToTable("test_records");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.AdditionalProperties);
        });

        // === B2 四码表（同构）+ 规则 + 技术要求 ===
        CatalogEntity(b.Entity<InspectionBrand>(), "inspection_brands");
        CatalogEntity(b.Entity<InspectionModel>(), "inspection_models");
        CatalogEntity(b.Entity<InspectionSpec>(), "inspection_specs");
        CatalogEntity(b.Entity<InspectionGrade>(), "inspection_grades");

        b.Entity<CalculationMethod>(e =>
        {
            e.ToTable("inspection_calculation_methods");
            e.HasKey(x => new { x.InspectionObjectCode, x.InspectionParameterCode });
            e.Property(x => x.AlgorithmType).HasConversion(Wire<CalculationAlgorithmType>());
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<TechnicalRequirement>(e =>
        {
            e.ToTable("inspection_technical_requirements");
            // SQL PK = 业务三键（tenant_id 是 V012 后加的隔离列，不进 PK；镜像 SSOT）
            e.HasKey(x => new { x.InspectionObjectCode, x.InspectionParameterCode, x.JudgmentStandardCode });
            e.Property(x => x.ValueType).HasConversion(Wire<RequirementValueType>());
            e.Property(x => x.Comparison).HasConversion(Wire<RequirementComparison>());
            e.Property(x => x.JudgmentMode).HasConversion(Wire<RequirementJudgmentMode>());
            e.Property(x => x.VerificationStatus).HasConversion(Wire<RequirementVerificationStatus>());
            e.Ignore(x => x.AdditionalProperties);
        });

        // === B5 字典 + B6 objects（平台级，无 tenant） ===
        b.Entity<InspectionSpecialty>(e =>
        {
            e.ToTable("inspection_specialties");
            e.HasKey(x => x.Code);
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<InspectionObject>(e =>
        {
            e.ToTable("inspection_objects");
            e.HasKey(x => x.Code);
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<InspectionParameter>(e =>
        {
            e.ToTable("inspection_parameters");
            e.HasKey(x => x.Code);
            e.Property(x => x.Aliases).HasColumnType("jsonb");
            e.Property(x => x.SourceType).HasConversion(Wire<InspectionParameterSourceType>());
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<InspectionStandard>(e =>
        {
            e.ToTable("inspection_standards");
            e.HasKey(x => x.Code);
            e.Property(x => x.Status).HasConversion(Wire<InspectionStandardStatus>());
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<InspectionReportName>(e =>
        {
            e.ToTable("inspection_report_names");
            e.HasKey(x => x.Code);
            e.Property(x => x.ExtFields).HasColumnType("jsonb");
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<ParamInterface>(e =>
        {
            e.ToTable("param_interfaces");
            e.HasKey(x => x.Code);
            e.Property(x => x.Config).HasColumnType("jsonb");
            e.Ignore(x => x.AdditionalProperties);
        });

        // === B6 八组 junction（PK 镜像 SQL；role 在两组 PK 里） ===
        b.Entity<SpecialtyObjectLink>(e =>
        {
            e.ToTable("inspection_specialty_objects");
            e.HasKey(x => new { x.InspectionSpecialtyCode, x.InspectionObjectCode });
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<ObjectParameterLink>(e =>
        {
            e.ToTable("inspection_object_parameters");
            e.HasKey(x => new { x.InspectionObjectCode, x.InspectionParameterCode });
            e.Property(x => x.QualificationLevel).HasConversion(Wire<QualificationLevel>());
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<ObjectStandardLink>(e =>
        {
            e.ToTable("inspection_object_standards");
            e.HasKey(x => new { x.InspectionObjectCode, x.InspectionStandardCode, x.Role });
            e.Property(x => x.Role).HasConversion(Wire<InspectionStandardRole>());
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<StandardParameterLink>(e =>
        {
            e.ToTable("inspection_standard_parameters");
            e.HasKey(x => new { x.InspectionStandardCode, x.InspectionParameterCode });
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<ObjectReportNameLink>(e =>
        {
            e.ToTable("inspection_object_report_names");
            e.HasKey(x => new { x.InspectionObjectCode, x.ReportNameCode });
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<ReportNameStandardLink>(e =>
        {
            e.ToTable("inspection_report_name_standards");
            e.HasKey(x => new { x.ReportNameCode, x.InspectionStandardCode, x.Role });
            e.Property(x => x.Role).HasConversion(Wire<InspectionStandardRole>());
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<ReportNameParameterLink>(e =>
        {
            e.ToTable("inspection_report_name_parameters");
            e.HasKey(x => new { x.ReportNameCode, x.InspectionParameterCode });
            e.Ignore(x => x.AdditionalProperties);
        });
        b.Entity<ParamInterfaceLink>(e =>
        {
            e.ToTable("param_interface_links");
            e.HasKey(x => new { x.InspectionParameterCode, x.ParamInterfaceCode });
            e.Property(x => x.Config).HasColumnType("jsonb");
            e.Ignore(x => x.AdditionalProperties);
        });
    }

    private static void CatalogEntity<TEntity>(EntityTypeBuilder<TEntity> e, string table)
        where TEntity : class
    {
        e.ToTable(table);
        e.HasKey("Code"); // V004 PK=code（V012 tenant_id 同租户唯一索引在 DB 侧）
    }

    // === 枚举 wire 转换器：值 = [EnumMember]（契约小写串），与 EnumMemberEnumConverter 同源 ===

    private static readonly Dictionary<Type, object> WireCache = new();

    private static ValueConverter<T, string> Wire<T>()
        where T : struct, Enum
    {
        if (WireCache.TryGetValue(typeof(T), out var cached))
        {
            return (ValueConverter<T, string>)cached;
        }

        var toWire = ToWireMap<T>();
        var fromWire = TolerantFromWire<T>();
        var converter = new ValueConverter<T, string>(
            v => toWire(v),
            s => fromWire(s));
        WireCache[typeof(T)] = converter;
        return converter;
    }

    private static Func<T, string> ToWireMap<T>()
        where T : struct, Enum
    {
        var map = new Dictionary<T, string>();
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            map[(T)field.GetValue(null)!] =
                field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;
        }

        return v => map.TryGetValue(v, out var wire)
            ? wire
            : throw new InvalidOperationException($"{typeof(T).Name}.{v} has no [EnumMember] mapping");
    }

    /// <summary>读容忍：wire 值优先，兜底 C# 成员名（存量数据/手写 SQL 可能写成员名）。</summary>
    private static Func<string, T> TolerantFromWire<T>()
        where T : struct, Enum
    {
        var byWire = new Dictionary<string, T>(StringComparer.Ordinal);
        var byName = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (T)field.GetValue(null)!;
            var wire = field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;
            byWire[wire] = value;
            byName[field.Name] = value;
        }

        return s => byWire.TryGetValue(s, out var v)
            ? v
            : byName.TryGetValue(s, out var byFallback)
                ? byFallback
                : throw new InvalidOperationException($"unknown {typeof(T).Name} value '{s}'");
    }

    // === issuedAt：DTO string &lt;-&gt; timestamptz（服务层从不写入非空值，仅双向兼容） ===

    private static readonly ValueConverter<string, DateTime> IsoDateTime = new(
        v => DateTime.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        v => v.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
}
