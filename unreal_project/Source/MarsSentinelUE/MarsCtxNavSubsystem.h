#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "MarsCtxNavSubsystem.generated.h"

USTRUCT(BlueprintType)
struct FMarsRouteMetrics
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadOnly, Category = "Mars|Navigation")
    FString RouteMode;

    UPROPERTY(BlueprintReadOnly, Category = "Mars|Navigation")
    float DistanceMeters = 0.0f;

    UPROPERTY(BlueprintReadOnly, Category = "Mars|Navigation")
    float MaxSlopeDeg = 0.0f;

    UPROPERTY(BlueprintReadOnly, Category = "Mars|Navigation")
    TArray<FVector> WaypointsCm;
};

/**
 * Shared NASA CTX 20m A* Navigation & Solar/RF Telemetry Subsystem for Unreal Engine 5.8.
 * Loads precomputed Shortest vs. Safest Jezero Crater routes from nav-module/routes.json
 * and converts metric coordinates directly into Unreal Engine centimeter space (1m = 100cm).
 */
UCLASS()
class UMarsCtxNavSubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;

    UFUNCTION(BlueprintCallable, Category = "Mars|Navigation")
    bool LoadRoutesFromJson(const FString& RelativeJsonPath);

    UPROPERTY(BlueprintReadOnly, Category = "Mars|Navigation")
    FMarsRouteMetrics ShortestRoute;

    UPROPERTY(BlueprintReadOnly, Category = "Mars|Navigation")
    FMarsRouteMetrics SafestRoute;
};
