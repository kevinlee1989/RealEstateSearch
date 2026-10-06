// Calls RealEstateSearch.Api directly from the browser (allowed by its CORS policy)
const API = "http://localhost:5241/api/listings";
const PAGE_SIZE = 20;

let page = 1;

const $ = (id) => document.getElementById(id);

// San Diego
const map = L.map("map").setView([32.78, -117.2], 11);
L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
  attribution: "&copy; OpenStreetMap contributors",
}).addTo(map);
const markers = L.layerGroup().addTo(map);

// Location picker: GET /api/listings/neighbourhoods
async function loadNeighbourhoods() {
  const response = await fetch(`${API}/neighbourhoods`);
  const neighbourhoods = await response.json();

  for (const n of neighbourhoods) {
    $("neighbourhood").add(new Option(`${n.name} (${n.count})`, n.name));
  }
}

// Builds the query string; empty inputs are left out so the API applies no filter for them
function buildQuery() {
  const params = new URLSearchParams();
  const mode = document.querySelector("input[name=mode]:checked").value;

  if (mode === "neighbourhood" && $("neighbourhood").value) {
    params.set("neighbourhood", $("neighbourhood").value);
  }

  if (mode === "map") {
    const bounds = map.getBounds();
    params.set("minLat", bounds.getSouth());
    params.set("maxLat", bounds.getNorth());
    params.set("minLon", bounds.getWest());
    params.set("maxLon", bounds.getEast());
  }

  for (const id of ["minPrice", "maxPrice", "roomType", "guests", "stay"]) {
    if ($(id).value) {
      params.set(id, $(id).value);
    }
  }

  params.set("sort", $("sort").value);
  params.set("page", page);
  params.set("pageSize", PAGE_SIZE);
  return params;
}

// GET /api/listings/search
async function search() {
  $("error").textContent = "";

  const response = await fetch(`${API}/search?${buildQuery()}`);
  const body = await response.json();

  if (!response.ok) {
    // 400 from the API's validation: show each field's messages
    const messages = body.errors
      ? Object.values(body.errors).flat()
      : [body.title];
    $("error").textContent = [...new Set(messages)].join("\n");
    return;
  }

  render(body);
}

function render({ total, items }) {
  const lastPage = Math.max(1, Math.ceil(total / PAGE_SIZE));

  $("total").textContent = `${total} listings`;
  $("page-info").textContent = `${page} / ${lastPage}`;
  $("prev").disabled = page <= 1;
  $("next").disabled = page >= lastPage;

  $("results").innerHTML = "";
  markers.clearLayers();

  for (const item of items) {
    const row = $("results").insertRow();
    for (const value of [
      item.name,
      item.neighbourhood,
      item.roomType,
      `$${item.pricePerNight}`,
      item.accommodates,
      item.minimumNights,
      item.reviewScoreRating ?? "-",
    ]) {
      // textContent, not innerHTML: listing names come from user data
      row.insertCell().textContent = value;
    }

    L.marker([item.latitude, item.longitude])
      .bindPopup(`${item.name}<br>$${item.pricePerNight}`)
      .addTo(markers);
  }
}

// A new search starts from page 1; Prev/Next keep the same filters
$("search-form").addEventListener("submit", (event) => {
  event.preventDefault();
  page = 1;
  search();
});
$("prev").addEventListener("click", () => { page--; search(); });
$("next").addEventListener("click", () => { page++; search(); });

loadNeighbourhoods().then(search);
