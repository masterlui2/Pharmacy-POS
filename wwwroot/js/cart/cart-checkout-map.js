(() => {
  let loaderPromise;
  let activeMap;

  const scriptId = "safemed-google-maps-script";
  const authFailureMessage =
    "Google Maps authorization failed. Use a valid API key with billing, Maps JavaScript API, Places API, and the correct allowed referrers.";

  const loadGoogleMaps = (apiKey) => {
    if (window.SafeMedGoogleMapsState?.status === "failed") {
      return Promise.reject(
        new Error(window.SafeMedGoogleMapsState.error || authFailureMessage),
      );
    }

    if (window.google?.maps) {
      return Promise.resolve(window.google.maps);
    }

    if (loaderPromise) {
      return loaderPromise;
    }

    if (!apiKey) {
      return Promise.reject(new Error("Google Maps API key is missing."));
    }

    loaderPromise = new Promise((resolve, reject) => {
      let settled = false;

      const fail = (message) => {
        if (settled) {
          return;
        }

        settled = true;
        loaderPromise = null;
        window.SafeMedGoogleMapsState = {
          status: "failed",
          error: message,
        };
        reject(new Error(message));
      };

      const succeed = () => {
        if (settled) {
          return;
        }

        if (window.SafeMedGoogleMapsState?.status === "failed") {
          fail(window.SafeMedGoogleMapsState.error || authFailureMessage);
          return;
        }

        if (!window.google?.maps) {
          fail("Unable to load Google Maps.");
          return;
        }

        settled = true;
        window.SafeMedGoogleMapsState = { status: "ready", error: "" };
        resolve(window.google.maps);
      };

      window.gm_authFailure = () => fail(authFailureMessage);

      const existing = document.getElementById(scriptId);
      if (existing) {
        existing.addEventListener("load", succeed, { once: true });
        existing.addEventListener("error", () => fail("Unable to load Google Maps."), { once: true });
        return;
      }

      const script = document.createElement("script");
      script.id = scriptId;
      script.async = true;
      script.defer = true;
      script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`;
      script.onload = succeed;
      script.onerror = () => fail("Unable to load Google Maps.");
      document.head.appendChild(script);
    });

    return loaderPromise;
  };

  const toRadians = (degrees) => degrees * (Math.PI / 180);

  const calculateDistanceKm = (origin, destination) => {
    const earthRadiusKm = 6371;
    const latitudeDelta = toRadians(destination.lat - origin.lat);
    const longitudeDelta = toRadians(destination.lng - origin.lng);
    const startLatitude = toRadians(origin.lat);
    const endLatitude = toRadians(destination.lat);
    const a =
      Math.sin(latitudeDelta / 2) ** 2 +
      Math.cos(startLatitude) *
        Math.cos(endLatitude) *
        Math.sin(longitudeDelta / 2) ** 2;

    return earthRadiusKm * (2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a)));
  };

  const buildDeliveryQuote = (settings, shippingOption, position) => {
    const branch = { lat: settings.branchLatitude, lng: settings.branchLongitude };
    const distanceKm = calculateDistanceKm(branch, position);
    const extraKm = Math.max(0, Math.ceil(distanceKm - settings.baseDistanceKm));
    const zoneFee = settings.baseFee + extraKm * settings.perKmFee;
    const surcharge = shippingOption === "Express" ? 45 : 0;
    const totalFee = zoneFee + surcharge;
    const inCoverage = distanceKm <= settings.maxRadiusKm;

    return {
      distanceKm,
      zoneFee,
      totalFee,
      inCoverage,
      coverageStatus: inCoverage ? "covered" : "blocked",
      coverageLabel: inCoverage
        ? `Within Davao delivery coverage (${distanceKm.toFixed(1)} km from ${settings.branchName})`
        : `Outside delivery coverage. Service is limited to ${settings.maxRadiusKm.toFixed(0)} km within Davao City.`,
    };
  };

  const teardown = () => {
    if (!activeMap) {
      return;
    }

    if (activeMap.listeners) {
      activeMap.listeners.forEach((listener) => listener.remove());
    }

    activeMap = null;
  };

  const syncCoverage = (draft, settings) => {
    if (
      typeof draft.address.latitude !== "number" ||
      typeof draft.address.longitude !== "number"
    ) {
      draft.address.coverageStatus = "unselected";
      draft.address.coverageLabel = "Pick a location within Davao City to continue.";
      draft.address.distanceKm = null;
      draft.address.deliveryFee = 0;
      return null;
    }

    const quote = buildDeliveryQuote(settings, draft.shipping.option, {
      lat: draft.address.latitude,
      lng: draft.address.longitude,
    });

    draft.address.distanceKm = quote.distanceKm;
    draft.address.deliveryFee = quote.totalFee;
    draft.address.coverageStatus = quote.coverageStatus;
    draft.address.coverageLabel = quote.coverageLabel;
    return quote;
  };

  const reverseGeocodeLocation = async (geocoder, location) => {
    if (!geocoder) {
      throw new Error("Unable to resolve a formatted address for this location.");
    }

    try {
      const response = await geocoder.geocode({ location });
      const formattedAddress = response.results?.[0]?.formatted_address?.trim();
      if (!formattedAddress) {
        throw new Error("No formatted address was returned for this location.");
      }

      return formattedAddress;
    } catch {
      throw new Error("Unable to resolve a formatted address for this location.");
    }
  };

  const mount = async ({
    root,
    searchInput,
    statusTarget,
    draft,
    settings,
    onChange,
    onError,
  }) => {
    teardown();

    const maps = await loadGoogleMaps(settings.apiKey);
    const center = {
      lat: draft.address.latitude ?? settings.branchLatitude,
      lng: draft.address.longitude ?? settings.branchLongitude,
    };
    const map = new maps.Map(root, {
      center,
      zoom: draft.address.latitude ? 15 : 12,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
    });
    const geocoder = new maps.Geocoder();
    const marker = new maps.Marker({
      map,
      position: center,
      draggable: true,
      animation: maps.Animation.DROP,
    });

    new maps.Circle({
      strokeColor: "#ef1c25",
      strokeOpacity: 0.85,
      strokeWeight: 1.5,
      fillColor: "#ef1c25",
      fillOpacity: 0.08,
      map,
      center: { lat: settings.branchLatitude, lng: settings.branchLongitude },
      radius: settings.maxRadiusKm * 1000,
    });

    const applySelection = async (location) => {
      marker.setPosition(location);
      map.panTo(location);

      let selectedAddress = searchInput?.value?.trim() || "";

      try {
        const response = await geocoder.geocode({ location });
        selectedAddress = response.results?.[0]?.formatted_address?.trim() || selectedAddress;
      } catch {
        selectedAddress = selectedAddress || `Pinned location: ${location.lat.toFixed(5)}, ${location.lng.toFixed(5)}`;
      }

      const quote = buildDeliveryQuote(settings, draft.shipping.option, location);
      onChange({
        deliveryAddress: selectedAddress,
        latitude: location.lat,
        longitude: location.lng,
        distanceKm: quote.distanceKm,
        deliveryFee: quote.totalFee,
        coverageStatus: quote.coverageStatus,
        coverageLabel: quote.coverageLabel,
      });

      if (searchInput && selectedAddress) {
        searchInput.value = selectedAddress;
      }

      if (statusTarget) {
        statusTarget.textContent = quote.coverageLabel;
      }
    };

    const autocomplete =
      searchInput
        ? new maps.places.Autocomplete(searchInput, {
            componentRestrictions: { country: "ph" },
            fields: ["formatted_address", "geometry"],
          })
        : null;

    const listeners = [
      map.addListener("click", (event) => {
        if (!event.latLng) {
          return;
        }

        applySelection({
          lat: event.latLng.lat(),
          lng: event.latLng.lng(),
        });
      }),
      marker.addListener("dragend", (event) => {
        if (!event.latLng) {
          return;
        }

        applySelection({
          lat: event.latLng.lat(),
          lng: event.latLng.lng(),
        });
      }),
    ];

    if (autocomplete) {
      listeners.push(
        autocomplete.addListener("place_changed", () => {
          const place = autocomplete.getPlace();
          const location = place.geometry?.location;
          if (!location) {
            onError("No map result matched that search.", "warning");
            return;
          }

          applySelection({
            lat: location.lat(),
            lng: location.lng(),
          });
        }),
      );
    }

    activeMap = { listeners, geocoder };

    if (draft.address.latitude && draft.address.longitude) {
      if (searchInput && draft.address.deliveryAddress) {
        searchInput.value = draft.address.deliveryAddress;
      }

      const quote = syncCoverage(draft, settings);
      if (statusTarget && quote) {
        statusTarget.textContent = quote.coverageLabel;
      }
    }
  };

  const locateUser = (settings, onSuccess, onError) => {
    if (!navigator.geolocation) {
      onError("This browser does not support location access.", "warning");
      return;
    }

    navigator.geolocation.getCurrentPosition(
      async (position) => {
        const location = {
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        };
        try {
          let geocoder = activeMap?.geocoder ?? null;
          if (!geocoder) {
            const maps = await loadGoogleMaps(settings.apiKey);
            geocoder = new maps.Geocoder();
          }

          const deliveryAddress = await reverseGeocodeLocation(geocoder, location);
          onSuccess({
            ...location,
            deliveryAddress,
          });
        } catch (error) {
          onError(
            error instanceof Error && error.message
              ? error.message
              : "Unable to resolve a formatted address for this location.",
            "warning",
          );
        }
      },
      () => {
        onError("Location access was blocked. You can search or pin the map manually.", "warning");
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
      },
    );
  };

  window.SafeMedCheckoutMap = {
    mount,
    teardown,
    syncCoverage,
    locateUser,
  };
})();
