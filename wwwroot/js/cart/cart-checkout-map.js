(() => {
  let loaderPromise;
  let activeMap;

  const scriptId = "safemed-google-maps-script";
  const mapsCallbackName = "SafeMedGoogleMapsLoaded";
  const authFailureMessage =
    "Google Maps authorization failed. Check the API key, billing, Maps JavaScript API, and allowed referrers.";
  const autocompleteUnavailableMessage =
    "Search suggestions are unavailable. You can still click or drag the map pin to choose the delivery location.";

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
        window[mapsCallbackName] = undefined;
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
        window[mapsCallbackName] = undefined;
        resolve(window.google.maps);
      };

      window.gm_authFailure = () => fail(authFailureMessage);
      window[mapsCallbackName] = succeed;

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
      const params = new URLSearchParams({
        key: apiKey,
        loading: "async",
        callback: mapsCallbackName,
        v: "weekly",
      });
      script.src = `https://maps.googleapis.com/maps/api/js?${params.toString()}`;
      script.onerror = () => fail("Unable to load Google Maps.");
      document.head.appendChild(script);
    });

    return loaderPromise;
  };

  const loadOptionalLibrary = async (maps, libraryName) => {
    if (typeof maps.importLibrary !== "function") {
      return null;
    }

    try {
      return await maps.importLibrary(libraryName);
    } catch {
      return null;
    }
  };

  const buildCoordinateLabel = (location) =>
    `Pinned location: ${location.lat.toFixed(5)}, ${location.lng.toFixed(5)}`;

  const normalizeLocation = (location) => {
    if (!location) {
      return null;
    }

    const lat =
      typeof location.lat === "function"
        ? location.lat()
        : Number.parseFloat(location.lat);
    const lng =
      typeof location.lng === "function"
        ? location.lng()
        : Number.parseFloat(location.lng);

    return Number.isFinite(lat) && Number.isFinite(lng) ? { lat, lng } : null;
  };

  const createGeocoder = async (maps) => {
    const geocodingLibrary = await loadOptionalLibrary(maps, "geocoding");
    const Geocoder = geocodingLibrary?.Geocoder || maps.Geocoder;
    return Geocoder ? new Geocoder() : null;
  };

  const createMarkerAdapter = (maps, markerLibrary, map, position) => {
    const AdvancedMarkerElement =
      markerLibrary?.AdvancedMarkerElement || maps.marker?.AdvancedMarkerElement;

    if (AdvancedMarkerElement) {
      const marker = new AdvancedMarkerElement({
        map,
        position,
        title: "Delivery location",
        gmpDraggable: true,
      });

      return {
        setPosition: (location) => {
          marker.position = location;
        },
        addDragEndListener: (handler) =>
          marker.addListener("dragend", (event) => {
            const location =
              normalizeLocation(event?.latLng) || normalizeLocation(marker.position);
            if (location) {
              handler(location);
            }
          }),
        remove: () => {
          marker.map = null;
        },
      };
    }

    if (maps.Marker) {
      const marker = new maps.Marker({
        map,
        position,
        draggable: true,
        animation: maps.Animation?.DROP,
      });

      return {
        setPosition: (location) => marker.setPosition(location),
        addDragEndListener: (handler) =>
          marker.addListener("dragend", (event) => {
            const location =
              normalizeLocation(event?.latLng) || normalizeLocation(marker.getPosition?.());
            if (location) {
              handler(location);
            }
          }),
        remove: () => marker.setMap(null),
      };
    }

    throw new Error("Google Maps marker support is unavailable.");
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

  const buildDeliveryQuote = (settings, position) => {
    const branch = { lat: settings.branchLatitude, lng: settings.branchLongitude };
    const distanceKm = calculateDistanceKm(branch, position);
    const extraKm = Math.max(0, Math.ceil(distanceKm - settings.baseDistanceKm));
    const zoneFee = settings.baseFee + extraKm * settings.perKmFee;
    const totalFee = zoneFee;
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

    activeMap.marker?.remove();
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

    const quote = buildDeliveryQuote(settings, {
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
    const [mapsLibrary, markerLibrary] = await Promise.all([
      loadOptionalLibrary(maps, "maps"),
      loadOptionalLibrary(maps, "marker"),
    ]);
    const MapClass = mapsLibrary?.Map || maps.Map;
    const CircleClass = mapsLibrary?.Circle || maps.Circle;
    if (!MapClass) {
      throw new Error("Google Maps map support is unavailable.");
    }

    const center = {
      lat: draft.address.latitude ?? settings.branchLatitude,
      lng: draft.address.longitude ?? settings.branchLongitude,
    };
    const mapOptions = {
      center,
      zoom: draft.address.latitude ? 15 : 12,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
    };
    const mapId = settings.mapId?.trim() || "DEMO_MAP_ID";
    if (mapId) {
      mapOptions.mapId = mapId;
    }

    const map = new MapClass(root, mapOptions);
    const geocoder = await createGeocoder(maps);
    const marker = createMarkerAdapter(maps, markerLibrary, map, center);

    if (CircleClass) {
      new CircleClass({
        strokeColor: "#ef1c25",
        strokeOpacity: 0.85,
        strokeWeight: 1.5,
        fillColor: "#ef1c25",
        fillOpacity: 0.08,
        map,
        center: { lat: settings.branchLatitude, lng: settings.branchLongitude },
        radius: settings.maxRadiusKm * 1000,
      });
    }

    const applySelection = async (location) => {
      marker.setPosition(location);
      map.panTo(location);

      let selectedAddress = searchInput?.value?.trim() || "";

      try {
        if (geocoder) {
          const response = await geocoder.geocode({ location });
          selectedAddress = response.results?.[0]?.formatted_address?.trim() || selectedAddress;
        }
      } catch {
        selectedAddress = selectedAddress || buildCoordinateLabel(location);
      }

      selectedAddress = selectedAddress || buildCoordinateLabel(location);
      const quote = buildDeliveryQuote(settings, location);
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

    let autocomplete = null;
    if (searchInput) {
      const placesLibrary = await loadOptionalLibrary(maps, "places");
      const Autocomplete =
        placesLibrary?.Autocomplete || maps.places?.Autocomplete || null;

      if (Autocomplete) {
        try {
          autocomplete = new Autocomplete(searchInput, {
            componentRestrictions: { country: "ph" },
            fields: ["formatted_address", "geometry"],
          });
        } catch {
          onError(autocompleteUnavailableMessage, "warning");
        }
      } else {
        onError(autocompleteUnavailableMessage, "warning");
      }
    }

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
      marker.addDragEndListener(applySelection),
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

    activeMap = { listeners, geocoder, marker };

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
        let deliveryAddress = buildCoordinateLabel(location);

        try {
          let geocoder = activeMap?.geocoder ?? null;
          if (!geocoder) {
            const maps = await loadGoogleMaps(settings.apiKey);
            geocoder = await createGeocoder(maps);
          }

          if (geocoder) {
            deliveryAddress = await reverseGeocodeLocation(geocoder, location);
          }
        } catch (error) {
          onError(
            error instanceof Error && error.message
              ? error.message
              : "Unable to resolve a formatted address for this location.",
            "warning",
          );
        }

        onSuccess({
          ...location,
          deliveryAddress,
        });
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
