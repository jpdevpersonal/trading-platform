/** @type {import("next").NextConfig} */
const config = {
  agentRules: false,
  allowedDevOrigins: ["127.0.0.1"],
  turbopack: { root: import.meta.dirname },
  devIndicators: false,
};

export default config;
