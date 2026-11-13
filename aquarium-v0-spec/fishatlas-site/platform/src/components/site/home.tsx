"use client";
import { useState } from "react";
import type { Locale } from "@/domain/types";
import { text } from "./shared";
export function HomeHero({ locale }: { locale: Locale }) {
  const [missing, setMissing] = useState(false);
  return (
    <div className={`hero-photo ${missing ? "hero-photo-missing" : ""}`}>
      {!missing ? (
        <img
          src="/media/photos/aquascape.jpg"
          alt={text(
            locale,
            "淡水水草缸中的植物与沉木",
            "Aquatic plants and driftwood in a freshwater aquarium",
          )}
          width="1200"
          height="900"
          fetchPriority="high"
          onError={() => setMissing(true)}
        />
      ) : (
        <div className="hero-fallback">
          <span>FishAtlas</span>
          <p>
            {text(
              locale,
              "认识每一种生命的日常。",
              "Discover the lives beneath the surface.",
            )}
          </p>
        </div>
      )}
    </div>
  );
}
