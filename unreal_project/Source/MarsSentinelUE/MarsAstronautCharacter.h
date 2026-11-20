#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "MarsAstronautCharacter.generated.h"

UENUM(BlueprintType)
enum class EMarsStoryChapter : uint8
{
    Ch1_DustfallArrival        UMETA(DisplayName = "Ch 1: Dustfall Arrival"),
    Ch2_CheyavaBiosignature    UMETA(DisplayName = "Ch 2: Cheyava Falls Biosignature"),
    Ch3_SolarFlareTriage       UMETA(DisplayName = "Ch 3: Solar Flare & Dust Storm Triage"),
    Ch4_BelvaRelayAndMeteorite UMETA(DisplayName = "Ch 4: Belva Rim Relay & Cacao Meteorite"),
    Ch5_SampleReturnIgnition   UMETA(DisplayName = "Ch 5: Mars Sample Return Ignition"),
    Epilogue_Complete          UMETA(DisplayName = "Epilogue: Sol 1042 Complete")
};

USTRUCT(BlueprintType)
struct FMarsSurvivalTelemetry
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    float OxygenPercent = 100.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    float PowerPercent = 96.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    float RadiationTraumaPercent = 4.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    float CumulativeRadMsv = 0.12f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    float DustOpticalDepthTau = 0.45f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    int32 CollectedCores = 0;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    bool bIsInsideShelter = false;
};

/**
 * Playable NASA EVA Astronaut Character for Unreal Engine 5.8 (Mars 0.38g = 3.72076 m/s^2).
 * Integrates Survival Telemetry (O2, Power, RAD, Beer-Lambert Dust Storm Tau),
 * RIMFAX Subsurface X-Ray Scanning [X], and 5-Chapter Story Progression ("Sol 1042: Echoes of Jezero").
 */
UCLASS(Blueprintable)
class AMarsAstronautCharacter : public ACharacter
{
    GENERATED_BODY()

public:
    AMarsAstronautCharacter();

    virtual void BeginPlay() override;
    virtual void Tick(float DeltaSeconds) override;

    UFUNCTION(BlueprintCallable, Category = "Mars|Story")
    void AdvanceStoryChapter();

    UFUNCTION(BlueprintCallable, Category = "Mars|Scanner")
    void TriggerRimfaxXRayScan();

    UFUNCTION(BlueprintCallable, Category = "Mars|Environment")
    void SetDustOpticalDepth(float NewTau);

    UFUNCTION(BlueprintPure, Category = "Mars|Environment")
    static float ComputeBeerLambertTransmission(float Tau, float SolarElevationDeg);

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Survival")
    FMarsSurvivalTelemetry Telemetry;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Story")
    EMarsStoryChapter CurrentChapter = EMarsStoryChapter::Ch1_DustfallArrival;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mars|Localization")
    bool bArabicLanguage = false;

    UFUNCTION(BlueprintPure, Category = "Mars|Story")
    FText GetActiveChapterDialogue() const;
};
