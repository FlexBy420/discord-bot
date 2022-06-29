﻿// <auto-generated />
using System;
using CompatBot.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace CompatBot.Migrations.HardwareDbMigrations
{
    [DbContext(typeof(HardwareDb))]
    partial class HardwareDbModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseCollation("NOCASE")
                .HasAnnotation("ProductVersion", "6.0.6");

            modelBuilder.Entity("CompatBot.Database.HwInfo", b =>
                {
                    b.Property<byte[]>("HwId")
                        .HasMaxLength(64)
                        .HasColumnType("BLOB")
                        .HasColumnName("hw_id");

                    b.Property<string>("CpuModel")
                        .HasColumnType("TEXT")
                        .HasColumnName("cpu_model");

                    b.Property<string>("GpuModel")
                        .HasColumnType("TEXT")
                        .HasColumnName("gpu_model");

                    b.Property<byte>("OsType")
                        .HasColumnType("INTEGER")
                        .HasColumnName("os_type");

                    b.Property<int>("CpuFeatures")
                        .HasColumnType("INTEGER")
                        .HasColumnName("cpu_features");

                    b.Property<string>("CpuMaker")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("cpu_maker");

                    b.Property<string>("GpuMaker")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("gpu_maker");

                    b.Property<int>("Id")
                        .HasColumnType("INTEGER")
                        .HasColumnName("id");

                    b.Property<string>("OsName")
                        .HasColumnType("TEXT")
                        .HasColumnName("os_name");

                    b.Property<string>("OsVersion")
                        .HasColumnType("TEXT")
                        .HasColumnName("os_version");

                    b.Property<long>("RamInMb")
                        .HasColumnType("INTEGER")
                        .HasColumnName("ram_in_mb");

                    b.Property<int>("ThreadCount")
                        .HasColumnType("INTEGER")
                        .HasColumnName("thread_count");

                    b.Property<long>("Timestamp")
                        .HasColumnType("INTEGER")
                        .HasColumnName("timestamp");

                    b.HasKey("HwId", "CpuModel", "GpuModel", "OsType")
                        .HasName("id");

                    b.HasIndex("Timestamp")
                        .HasDatabaseName("hardware_timestamp");

                    b.ToTable("hw_info");
                });
#pragma warning restore 612, 618
        }
    }
}
