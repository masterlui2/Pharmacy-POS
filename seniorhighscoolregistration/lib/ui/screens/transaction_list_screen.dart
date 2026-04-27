// lib/ui/screens/transaction_list_screen.dart

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../bloc/registration_bloc.dart';
import '../../bloc/registration_event.dart';
import '../../bloc/registration_state.dart';
import '../../data/csv_export_helper.dart';
import '../../data/models/registration_model.dart';
import '../widgets/registration_card.dart';
import 'package:seniorhighscoolregistration/ui/screens/registration_detail_screen.dart';

class TransactionListScreen extends StatefulWidget {
  const TransactionListScreen({super.key});

  @override
  State<TransactionListScreen> createState() => _TransactionListScreenState();
}

class _TransactionListScreenState extends State<TransactionListScreen> {
  final _searchController = TextEditingController();
  bool _showSearch = false;

  @override
  void initState() {
    super.initState();
    context.read<RegistrationBloc>().add(const LoadRegistrations());
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _onSearchChanged(String query) {
    context.read<RegistrationBloc>().add(SearchRegistrations(query));
  }

  void _clearSearch() {
    _searchController.clear();
    context.read<RegistrationBloc>().add(const LoadRegistrations());
    setState(() => _showSearch = false);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: _showSearch
            ? TextField(
          controller: _searchController,
          autofocus: true,
          onChanged: _onSearchChanged,
          decoration: InputDecoration(
            hintText: 'Search by name, staff, strand…',
            border: InputBorder.none,
            hintStyle: TextStyle(color: colorScheme.onSurface.withOpacity(0.4)),
          ),
          style: theme.textTheme.bodyLarge,
        )
            : const Text('Transactions'),
        centerTitle: false,
        elevation: 0,
        scrolledUnderElevation: 1,
        actions: [
          if (_showSearch)
            IconButton(
              icon: const Icon(Icons.close),
              tooltip: 'Clear search',
              onPressed: _clearSearch,
            )
          else ...[
            IconButton(
              icon: const Icon(Icons.search_outlined),
              tooltip: 'Search',
              onPressed: () => setState(() => _showSearch = true),
            ),
            IconButton(
              icon: const Icon(Icons.refresh_outlined),
              tooltip: 'Refresh',
              onPressed: () =>
                  context.read<RegistrationBloc>().add(const LoadRegistrations()),
            ),
            BlocBuilder<RegistrationBloc, RegistrationState>(
              builder: (context, state) {
                final regs = switch (state) {
                  RegistrationsLoaded() => state.registrations,
                  RegistrationSuccess() => state.registrations,
                  _ => <RegistrationModel>[],
                };
                return IconButton(
                  icon: const Icon(Icons.ios_share_outlined),
                  tooltip: 'Export CSV',
                  onPressed: regs.isEmpty
                      ? null
                      : () async {
                    try {
                      await CsvExportHelper.exportRegistrations(regs);
                    } catch (e) {
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(content: Text('Export failed: $e')),
                        );
                      }
                    }
                  },
                );
              },
            ),
          ],
        ],
      ),
      body: BlocConsumer<RegistrationBloc, RegistrationState>(
        listener: (context, state) {
          if (state is RegistrationSuccess) {
            context.read<RegistrationBloc>().add(const LoadRegistrations());
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.message),
                backgroundColor: Colors.green.shade700,
                behavior: SnackBarBehavior.floating,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
            );
          }
          if (state is RegistrationError) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.message),
                backgroundColor: colorScheme.error,
                behavior: SnackBarBehavior.floating,
              ),
            );
          }
        },
        builder: (context, state) {
          if (state is RegistrationLoading) {
            return const Center(child: CircularProgressIndicator());
          }

          final List<RegistrationModel> registrations = switch (state) {
            RegistrationsLoaded() => state.registrations,
            RegistrationSuccess() => state.registrations,
            _ => [],
          };

          final searchQuery = state is RegistrationsLoaded ? state.searchQuery : '';

          if (registrations.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    searchQuery.isNotEmpty ? Icons.search_off : Icons.inbox_outlined,
                    size: 72,
                    color: colorScheme.onSurfaceVariant.withOpacity(0.3),
                  ),
                  const SizedBox(height: 16),
                  Text(
                    searchQuery.isNotEmpty
                        ? 'No results for "$searchQuery"'
                        : 'No registrations yet',
                    style: theme.textTheme.titleMedium?.copyWith(
                      color: colorScheme.onSurfaceVariant.withOpacity(0.6),
                    ),
                  ),
                  if (searchQuery.isNotEmpty) ...[
                    const SizedBox(height: 8),
                    TextButton(
                      onPressed: _clearSearch,
                      child: const Text('Clear search'),
                    ),
                  ],
                ],
              ),
            );
          }

          return Column(
            children: [
              // Summary bar
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                color: colorScheme.surfaceContainerHighest.withOpacity(0.3),
                child: Row(
                  children: [
                    Icon(Icons.list_alt_outlined, size: 15, color: colorScheme.onSurfaceVariant),
                    const SizedBox(width: 6),
                    Text(
                      searchQuery.isNotEmpty
                          ? '${registrations.length} result${registrations.length == 1 ? '' : 's'} for "$searchQuery"'
                          : '${registrations.length} registration${registrations.length == 1 ? '' : 's'}',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: ListView.builder(
                  padding: const EdgeInsets.only(top: 8, bottom: 24),
                  itemCount: registrations.length,
                  itemBuilder: (context, index) {
                    return RegistrationCard(
                      registration: registrations[index],
                      onTap: () => Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => BlocProvider.value(
                            value: context.read<RegistrationBloc>(),
                            child: RegistrationDetailScreen(
                              registration: registrations[index],
                            ),
                          ),
                        ),
                      ).then((_) =>
                          context.read<RegistrationBloc>().add(const LoadRegistrations())),
                      onDelete: () => context
                          .read<RegistrationBloc>()
                          .add(DeleteRegistration(registrations[index].id!)),
                    );
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
