// lib/ui/screens/dashboard_screen.dart

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../bloc/registration_bloc.dart';
import '../../bloc/registration_event.dart';
import '../../bloc/registration_state.dart';
import '../../data/models/registration_model.dart';
import 'package:seniorhighscoolregistration/constants.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  @override
  void initState() {
    super.initState();
    context.read<RegistrationBloc>().add(const LoadDashboardStats());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Dashboard'),
        centerTitle: false,
        elevation: 0,
        scrolledUnderElevation: 1,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_outlined),
            onPressed: () =>
                context.read<RegistrationBloc>().add(const LoadDashboardStats()),
          ),
        ],
      ),
      body: BlocBuilder<RegistrationBloc, RegistrationState>(
        builder: (context, state) {
          if (state is RegistrationLoading) {
            return const Center(child: CircularProgressIndicator());
          }
          if (state is DashboardStatsLoaded) {
            return _buildDashboard(context, state);
          }
          if (state is RegistrationError) {
            return Center(child: Text(state.message));
          }
          return const Center(child: CircularProgressIndicator());
        },
      ),
    );
  }

  Widget _buildDashboard(BuildContext context, DashboardStatsLoaded state) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final recentRegs = state.registrations.take(5).toList();

    return RefreshIndicator(
      onRefresh: () async =>
          context.read<RegistrationBloc>().add(const LoadDashboardStats()),
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          // Total count hero
          _HeroCard(
            total: state.totalCount,
            colorScheme: colorScheme,
            theme: theme,
          ),

          const SizedBox(height: 20),

          // Strand breakdown
          Text('Strand Breakdown',
              style: theme.textTheme.titleMedium
                  ?.copyWith(fontWeight: FontWeight.w600)),
          const SizedBox(height: 12),
          _StrandBreakdownCard(
            strandCounts: state.strandCounts,
            total: state.totalCount,
            colorScheme: colorScheme,
            theme: theme,
          ),

          const SizedBox(height: 24),

          // Quick stats row
          Row(
            children: [
              Expanded(
                child: _QuickStatCard(
                  icon: Icons.people_outline,
                  label: 'Total',
                  value: '${state.totalCount}',
                  color: colorScheme.primary,
                  colorScheme: colorScheme,
                  theme: theme,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _QuickStatCard(
                  icon: Icons.attach_file,
                  label: 'With Docs',
                  value: '${state.registrations.where((r) => r.documentName != null).length}',
                  color: Colors.green,
                  colorScheme: colorScheme,
                  theme: theme,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _QuickStatCard(
                  icon: Icons.info_outline,
                  label: 'No Docs',
                  value: '${state.registrations.where((r) => r.documentName == null).length}',
                  color: Colors.orange,
                  colorScheme: colorScheme,
                  theme: theme,
                ),
              ),
            ],
          ),

          const SizedBox(height: 24),

          // Recent registrations
          if (recentRegs.isNotEmpty) ...[
            Text('Recent Registrations',
                style: theme.textTheme.titleMedium
                    ?.copyWith(fontWeight: FontWeight.w600)),
            const SizedBox(height: 12),
            ...recentRegs.map((reg) => _RecentRegTile(
                  registration: reg,
                  theme: theme,
                  colorScheme: colorScheme,
                )),
          ],
        ],
      ),
    );
  }
}

class _HeroCard extends StatelessWidget {
  final int total;
  final ColorScheme colorScheme;
  final ThemeData theme;

  const _HeroCard({required this.total, required this.colorScheme, required this.theme});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [colorScheme.primary, colorScheme.primary.withOpacity(0.7)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Total Students',
                    style: theme.textTheme.labelLarge?.copyWith(
                      color: Colors.white.withOpacity(0.8),
                    )),
                const SizedBox(height: 8),
                SizedBox(
                  width: 120,
                  child: Text('$total',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 48,
                        fontWeight: FontWeight.w700,
                        height: 1,
                      ),
                      textAlign: TextAlign.end)
                )
                ,
                const SizedBox(height: 4),
                Text('registered students',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: Colors.white.withOpacity(0.7),
                    )),
              ],
            ),
          ),
          Icon(Icons.school, size: 64, color: Colors.white.withOpacity(0.2)),
        ],
      ),
    );
  }
}

class _StrandBreakdownCard extends StatelessWidget {
  final Map<String, int> strandCounts;
  final int total;
  final ColorScheme colorScheme;
  final ThemeData theme;

  const _StrandBreakdownCard({
    required this.strandCounts,
    required this.total,
    required this.colorScheme,
    required this.theme,
  });

  Color _strandColor(String strand) {
    switch (strand) {
      case 'ABM': return Colors.blue;
      case 'STEM': return Colors.green;
      case 'GAS': return Colors.orange;
      case 'HUMS': return Colors.purple;
      default: return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    final strands = AppConstants.strands;

    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: colorScheme.outline.withOpacity(0.15)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: strands.map((strand) {
            final count = strandCounts[strand] ?? 0;
            final pct = total > 0 ? count / total : 0.0;
            final color = _strandColor(strand);

            return Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        width: 8, height: 8,
                        decoration: BoxDecoration(color: color, shape: BoxShape.circle),
                      ),
                      const SizedBox(width: 8),
                      Text(strand,
                          style: theme.textTheme.bodyMedium
                              ?.copyWith(fontWeight: FontWeight.w500)),
                      const Spacer(),
                      Text('$count',
                          style: theme.textTheme.bodyMedium
                              ?.copyWith(fontWeight: FontWeight.w600, color: color)),
                      const SizedBox(width: 4),
                      Text('(${(pct * 100).toStringAsFixed(0)}%)',
                          style: theme.textTheme.bodySmall
                              ?.copyWith(color: colorScheme.onSurfaceVariant)),
                    ],
                  ),
                  const SizedBox(height: 6),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: pct,
                      minHeight: 6,
                      backgroundColor: color.withOpacity(0.12),
                      valueColor: AlwaysStoppedAnimation(color),
                    ),
                  ),
                ],
              ),
            );
          }).toList(),
        ),
      ),
    );
  }
}

class _QuickStatCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;
  final Color color;
  final ColorScheme colorScheme;
  final ThemeData theme;

  const _QuickStatCard({
    required this.icon,
    required this.label,
    required this.value,
    required this.color,
    required this.colorScheme,
    required this.theme,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: colorScheme.outline.withOpacity(0.15)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          children: [
            Icon(icon, color: color, size: 24),
            const SizedBox(height: 6),
            Text(value,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: color,
                )),
            Text(label,
                style: theme.textTheme.labelSmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                )),
          ],
        ),
      ),
    );
  }
}

class _RecentRegTile extends StatelessWidget {
  final RegistrationModel registration;
  final ThemeData theme;
  final ColorScheme colorScheme;

  const _RecentRegTile({
    required this.registration,
    required this.theme,
    required this.colorScheme,
  });

  Color _strandColor(String strand) {
    switch (strand) {
      case 'ABM': return Colors.blue;
      case 'STEM': return Colors.green;
      case 'GAS': return Colors.orange;
      case 'HUMS': return Colors.purple;
      default: return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    final color = _strandColor(registration.strand);

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: colorScheme.outline.withOpacity(0.15)),
        color: colorScheme.surfaceContainerHighest.withOpacity(0.2),
      ),
      child: Row(
        children: [
          Container(
            width: 36, height: 36,
            decoration: BoxDecoration(
              color: color.withOpacity(0.12),
              borderRadius: BorderRadius.circular(8),
            ),
            alignment: Alignment.center,
            child: Text(registration.strand,
                style: TextStyle(color: color, fontSize: 9, fontWeight: FontWeight.w700)),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(registration.studentName,
                    style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
                Text(registration.date,
                    style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant)),
              ],
            ),
          ),
          if (registration.documentName != null)
            Icon(Icons.attach_file, size: 14, color: colorScheme.primary),
        ],
      ),
    );
  }
}
