#include "MarsCtxNavSubsystem.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

void UMarsCtxNavSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);
    LoadRoutesFromJson(TEXT("../nav-module/routes.json"));
}

bool UMarsCtxNavSubsystem::LoadRoutesFromJson(const FString& RelativeJsonPath)
{
    const FString FullPath = FPaths::ConvertRelativePathToFull(FPaths::ProjectDir(), RelativeJsonPath);
    FString JsonText;
    if (!FFileHelper::LoadFileToString(JsonText, *FullPath))
    {
        return false;
    }

    TSharedPtr<FJsonObject> RootObj;
    const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonText);
    if (!FJsonSerializer::Deserialize(Reader, RootObj) || !RootObj.IsValid())
    {
        return false;
    }

    const TArray<TSharedPtr<FJsonValue>>* Presets = nullptr;
    if (!RootObj->TryGetArrayField(TEXT("presets"), Presets) || Presets->Num() == 0)
    {
        return false;
    }

    const TSharedPtr<FJsonObject> P0 = (*Presets)[0]->AsObject();
    auto ParseMode = [&](const FString& Key, FMarsRouteMetrics& OutMetrics)
    {
        const TSharedPtr<FJsonObject>* ModeObj = nullptr;
        if (P0->TryGetObjectField(Key, ModeObj) && ModeObj && (*ModeObj).IsValid())
        {
            OutMetrics.RouteMode = Key;
            OutMetrics.DistanceMeters = static_cast<float>((*ModeObj)->GetNumberField(TEXT("distance_m")));
            OutMetrics.MaxSlopeDeg = static_cast<float>((*ModeObj)->GetNumberField(TEXT("max_slope_deg")));
            OutMetrics.WaypointsCm.Reset();
            const TArray<TSharedPtr<FJsonValue>>* Wpts = nullptr;
            if ((*ModeObj)->TryGetArrayField(TEXT("waypoints"), Wpts))
            {
                for (const TSharedPtr<FJsonValue>& W : *Wpts)
                {
                    const TSharedPtr<FJsonObject> WObj = W->AsObject();
                    if (!WObj.IsValid()) continue;
                    const float Col = static_cast<float>(WObj->GetNumberField(TEXT("col")));
                    const float Row = static_cast<float>(WObj->GetNumberField(TEXT("row")));
                    // Map CTX grid cells into local Jezero UE5 centimeter coordinates
                    OutMetrics.WaypointsCm.Add(FVector((Col - 200.0f) * 80.0f, (Row - 189.0f) * 80.0f, 50.0f));
                }
            }
        }
    };

    ParseMode(TEXT("shortest"), ShortestRoute);
    ParseMode(TEXT("safest"), SafestRoute);
    return ShortestRoute.WaypointsCm.Num() > 0 && SafestRoute.WaypointsCm.Num() > 0;
}
